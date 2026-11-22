using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 적 모델 → (뼈 자동 배치/가중치) → 대기·걷기·특수 클립 → Animator 컨트롤러 → 모델 프리팹(*_Rig) → 적 프리팹에 끼움.
/// 적 프리팹 구조: 루트(Enemy, Rigidbody, Capsule) / Visual(빈 피벗, Enemy.visual) / {Model}_Rig(Animator, EnemyAnimator).
/// 메뉴: Roguelike/Build Enemy Animations. 적별 수치는 EnemyRig 서브클래스.
/// </summary>
public static class EnemyRigBuilder
{
    const string Src = "Assets/Prototype/Creatures/ENTITY/ENEMY", Out = Src + "/Rig", Prefabs = "Assets/Prototype/Prefabs";
    const int Keys = 25;

    [MenuItem("Roguelike/Build Enemy Animations")]
    public static void BuildAll()
    {
        foreach (var r in EnemyRig.All) Build(r);
        AssetDatabase.SaveAssets();
        Debug.Log("[EnemyRig] 완료");
    }

    public static GameObject Build(EnemyRig r)
    {
        CreatureRigBuilder.EnsureFolder(Out);
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Src}/{r.Model}.fbx");
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{Src}/{r.Model}.mat");
        var rigRoot = new GameObject(r.Model + "_Rig");
        Bounds b;
        if (fbx.GetComponentInChildren<SkinnedMeshRenderer>()) b = WrapSkinned(rigRoot, fbx, mat);
        else b = BuildSkin(r, rigRoot, fbx, mat);

        // 클립 + 컨트롤러
        string dir = $"{Out}/{r.Model}";
        CreatureRigBuilder.EnsureFolder(dir);
        var idle = BuildClip(r, EnemyRig.Motion.Idle, r.IdleLen, true, rigRoot.transform, b, $"{dir}/{r.Model}_Idle.anim");
        var walk = BuildClip(r, EnemyRig.Motion.Walk, r.WalkLen, true, rigRoot.transform, b, $"{dir}/{r.Model}_Walk.anim");
        AnimationClip special = null;
        if (r.HasCharge) special = BuildClip(r, EnemyRig.Motion.Special, r.SpecialLen, true, rigRoot.transform, b, $"{dir}/{r.Model}_Charge.anim");
        else if (r.HasAbility) special = BuildClip(r, EnemyRig.Motion.Special, r.SpecialLen, false, rigRoot.transform, b, $"{dir}/{r.Model}_Ability.anim");
        var ctrl = BuildController(r, idle, walk, special, $"{dir}/{r.Model}_Animator.controller");

        var anim = rigRoot.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        rigRoot.AddComponent<EnemyAnimator>().Setup(anim, r.WalkRef, r.HasCharge, r.HasAbility);
        var rigPrefab = PrefabUtility.SaveAsPrefabAsset(rigRoot, $"{Out}/{r.Model}_Rig.prefab");
        Object.DestroyImmediate(rigRoot);

        InstallInto(r, rigPrefab, b);
        return rigPrefab;
    }

    // =====================================================================
    // 모델
    // =====================================================================
    /// <summary>이미 리깅된 모델(Meshy 리그): 그대로 감싸기.</summary>
    static Bounds WrapSkinned(GameObject root, GameObject fbx, Material mat)
    {
        var m = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
        var smr = m.GetComponentInChildren<SkinnedMeshRenderer>();
        if (mat) smr.sharedMaterial = mat;
        // 뼈 위치로 실제 크기 (스킨 경계 상자는 여유가 커서 쓰지 않음)
        var b = new Bounds(smr.rootBone.position, Vector3.zero);
        foreach (var t in smr.bones) b.Encapsulate(t.position);
        b.min = new Vector3(b.min.x, 0f, b.min.z);
        return b;
    }

    /// <summary>뼈 없는 모델: 정점을 굽고(FBX 회전/배율 제거, 발바닥 y=0, 가운데 정렬) 정의대로 뼈와 가중치를 만듦.</summary>
    static Bounds BuildSkin(EnemyRig r, GameObject root, GameObject fbx, Material mat)
    {
        var mf = fbx.GetComponentInChildren<MeshFilter>();
        var src = mf.sharedMesh;
        Matrix4x4 m = mf.transform.localToWorldMatrix;
        var sv = src.vertices; int n = sv.Length;
        var v = new Vector3[n];
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < n; i++) { v[i] = m.MultiplyPoint3x4(sv[i]); min = Vector3.Min(min, v[i]); max = Vector3.Max(max, v[i]); }
        var off = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
        for (int i = 0; i < n; i++) v[i] -= off;
        var b = new Bounds((min + max) * 0.5f - off, max - min);
        var nrm = src.normals; var tan = src.tangents;
        for (int i = 0; i < nrm.Length; i++) nrm[i] = m.MultiplyVector(nrm[i]).normalized;
        for (int i = 0; i < tan.Length; i++) { var t3 = m.MultiplyVector(tan[i]).normalized; tan[i] = new Vector4(t3.x, t3.y, t3.z, tan[i].w); }

        var spec = r.Skeleton(v, b);
        int bc = spec.Length;
        var tris = src.triangles;
        var adj = CreatureRigBuilder.Adjacency(v, tris);
        var raw = new float[n][];
        for (int i = 0; i < n; i++) { raw[i] = new float[bc]; r.Weigh(v[i], b, raw[i]); }
        if (r.IslandBone >= 0) MarkIslands(raw, adj, r.IslandBone, n / 30);
        CreatureRigBuilder.SmoothWeights(raw, adj, 2);
        var weights = new BoneWeight[n];
        for (int i = 0; i < n; i++) weights[i] = Top4(raw[i]);

        string dir = $"{Out}/{r.Model}";
        CreatureRigBuilder.EnsureFolder(dir);
        var mesh = new Mesh { name = r.Model + "_Mesh" };
        if (n > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = v; mesh.normals = nrm;
        if (tan.Length == n) mesh.tangents = tan;
        mesh.uv = src.uv;
        mesh.subMeshCount = src.subMeshCount;
        for (int k = 0; k < src.subMeshCount; k++) mesh.SetTriangles(src.GetTriangles(k), k);
        mesh.boneWeights = weights;
        var bind = new Matrix4x4[bc];
        for (int k = 0; k < bc; k++) bind[k] = Matrix4x4.Translate(-spec[k].pos);
        mesh.bindposes = bind;
        mesh.RecalculateBounds();
        mesh = CreatureRigBuilder.SaveAsset(mesh, $"{dir}/{r.Model}_Mesh.asset");

        var bones = new Transform[bc];
        for (int k = 0; k < bc; k++)
        {
            bones[k] = new GameObject(spec[k].name).transform;
            bones[k].SetParent(spec[k].parent < 0 ? root.transform : bones[spec[k].parent], false);
            bones[k].position = spec[k].pos;
        }
        var skin = new GameObject("Mesh");
        skin.transform.SetParent(root.transform, false);
        var smr = skin.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh; smr.bones = bones; smr.rootBone = bones[0];
        smr.sharedMaterial = mat ? mat : fbx.GetComponentInChildren<Renderer>().sharedMaterial;
        smr.localBounds = new Bounds(b.center - spec[0].pos + Vector3.up * b.size.y * 0.3f, b.size * 1.8f);
        return b;
    }

    /// <summary>작은 떨어진 덩어리(정점 maxSize개 미만)는 통째로 bone에.</summary>
    static void MarkIslands(float[][] w, int[][] adj, int bone, int maxSize)
    {
        int n = w.Length;
        var comp = new int[n];
        for (int i = 0; i < n; i++) comp[i] = -1;
        var stack = new Stack<int>(); var members = new List<int>();
        for (int s = 0, id = 0; s < n; s++)
        {
            if (comp[s] >= 0) continue;
            members.Clear(); stack.Push(s); comp[s] = id;
            while (stack.Count > 0)
            {
                int i = stack.Pop(); members.Add(i);
                foreach (int j in adj[i]) if (comp[j] < 0) { comp[j] = id; stack.Push(j); }
            }
            if (members.Count < maxSize)
                foreach (int i in members) { System.Array.Clear(w[i], 0, w[i].Length); w[i][bone] = 1f; }
            id++;
        }
    }

    static BoneWeight Top4(float[] w)
    {
        Span4 s = default;
        for (int i = 0; i < w.Length; i++) s.Add(i, w[i]);
        return s.ToWeight();
    }

    struct Span4
    {
        int i0, i1, i2, i3; float w0, w1, w2, w3;
        public void Add(int i, float w)
        {
            if (w <= w3) return;
            if (w > w0) { (i3, w3, i2, w2, i1, w1) = (i2, w2, i1, w1, i0, w0); i0 = i; w0 = w; }
            else if (w > w1) { (i3, w3, i2, w2) = (i2, w2, i1, w1); i1 = i; w1 = w; }
            else if (w > w2) { (i3, w3) = (i2, w2); i2 = i; w2 = w; }
            else { i3 = i; w3 = w; }
        }
        public BoneWeight ToWeight()
        {
            float s = w0 + w1 + w2 + w3;
            if (s <= 0f) return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            return new BoneWeight { boneIndex0 = i0, weight0 = w0 / s, boneIndex1 = i1, weight1 = w1 / s, boneIndex2 = i2, weight2 = w2 / s, boneIndex3 = i3, weight3 = w3 / s };
        }
    }

    // =====================================================================
    // 클립: 정의의 자세(쉬는 자세 기준 월드 축)를 각 뼈의 로컬 값으로 바꿔 키 생성
    // =====================================================================
    static AnimationClip BuildClip(EnemyRig r, EnemyRig.Motion motion, float len, bool loop, Transform root, Bounds b, string path)
    {
        var clip = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(path), frameRate = 30 };
        var pw = new PoseWriter();
        var bones = new Dictionary<string, Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
        var rk = new Dictionary<string, Keyframe[][]>(); var pk = new Dictionary<string, Keyframe[][]>(); var sk = new Dictionary<string, Keyframe[][]>();
        for (int k = 0; k < Keys; k++)
        {
            float t = k / (float)(Keys - 1), time = t * len;
            pw.Clear();
            r.Pose(motion, t, b, pw);
            foreach (var e in pw.rot)
            {
                var bt = bones[e.Key];
                Quaternion w = Quaternion.Inverse(root.rotation) * bt.rotation; // 쉬는 자세의 월드 회전 (루트 기준)
                Quaternion q = bt.localRotation * Quaternion.Inverse(w) * Quaternion.Euler(e.Value) * w;
                Put(rk, e.Key, 4, k, time, q.x, q.y, q.z, q.w);
            }
            foreach (var e in pw.move)
            {
                var bt = bones[e.Key];
                Vector3 d = bt.parent ? Quaternion.Inverse(Quaternion.Inverse(root.rotation) * bt.parent.rotation) * e.Value : e.Value;
                if (bt.parent) d = Vector3.Scale(d, Inv(bt.parent.lossyScale));
                Vector3 lp = bt.localPosition + d;
                Put(pk, e.Key, 3, k, time, lp.x, lp.y, lp.z, 0f);
            }
            foreach (var e in pw.scale)
            {
                Vector3 ls = Vector3.Scale(bones[e.Key].localScale, e.Value);
                Put(sk, e.Key, 3, k, time, ls.x, ls.y, ls.z, 0f);
            }
        }
        string[] rc = { "localRotation.x", "localRotation.y", "localRotation.z", "localRotation.w" };
        string[] pc = { "localPosition.x", "localPosition.y", "localPosition.z" };
        string[] sc = { "localScale.x", "localScale.y", "localScale.z" };
        Write(clip, root, bones, rk, rc); Write(clip, root, bones, pk, pc); Write(clip, root, bones, sk, sc);
        clip.EnsureQuaternionContinuity();
        var st = AnimationUtility.GetAnimationClipSettings(clip);
        st.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, st);
        return CreatureRigBuilder.SaveAsset(clip, path);
    }

    static Vector3 Inv(Vector3 s) => new(1f / s.x, 1f / s.y, 1f / s.z);

    static void Put(Dictionary<string, Keyframe[][]> d, string bone, int comps, int k, float time, float a, float b, float c, float e)
    {
        if (!d.TryGetValue(bone, out var arr))
        {
            arr = new Keyframe[comps][];
            for (int i = 0; i < comps; i++) arr[i] = new Keyframe[Keys];
            d[bone] = arr;
        }
        arr[0][k] = new Keyframe(time, a); arr[1][k] = new Keyframe(time, b); arr[2][k] = new Keyframe(time, c);
        if (comps == 4) arr[3][k] = new Keyframe(time, e);
    }

    static void Write(AnimationClip clip, Transform root, Dictionary<string, Transform> bones, Dictionary<string, Keyframe[][]> d, string[] props)
    {
        foreach (var e in d)
        {
            string path = AnimationUtility.CalculateTransformPath(bones[e.Key], root);
            for (int c = 0; c < props.Length; c++) clip.SetCurve(path, typeof(Transform), props[c], CreatureRigBuilder.Smoothed(e.Value[c]));
        }
    }

    // =====================================================================
    // 컨트롤러: Locomotion(대기↔걷기 블렌드, Speed) + Charge(돌진 반복, Charge bool) 또는 Ability(1회, 트리거)
    // =====================================================================
    static AnimatorController BuildController(EnemyRig r, AnimationClip idle, AnimationClip walk, AnimationClip special, string path)
    {
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter(EnemyAnimator.SpeedParam, AnimatorControllerParameterType.Float);
        var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree);
        tree.blendParameter = EnemyAnimator.SpeedParam;
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 1f);
        var sm = ctrl.layers[0].stateMachine;
        sm.defaultState = loco;
        if (special && r.HasCharge)
        {
            ctrl.AddParameter(EnemyAnimator.ChargeParam, AnimatorControllerParameterType.Bool);
            var st = sm.AddState("Charge"); st.motion = special;
            var go = loco.AddTransition(st); go.hasExitTime = false; go.duration = 0.08f; go.AddCondition(AnimatorConditionMode.If, 0f, EnemyAnimator.ChargeParam);
            var back = st.AddTransition(loco); back.hasExitTime = false; back.duration = 0.15f; back.AddCondition(AnimatorConditionMode.IfNot, 0f, EnemyAnimator.ChargeParam);
        }
        else if (special && r.HasAbility)
        {
            ctrl.AddParameter(EnemyAnimator.AbilityParam, AnimatorControllerParameterType.Trigger);
            var st = sm.AddState("Ability"); st.motion = special;
            var go = sm.AddAnyStateTransition(st); go.hasExitTime = false; go.duration = 0.05f; go.canTransitionToSelf = false;
            go.AddCondition(AnimatorConditionMode.If, 0f, EnemyAnimator.AbilityParam);
            var back = st.AddTransition(loco); back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.1f;
        }
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // =====================================================================
    // 적 프리팹에 끼우기
    // =====================================================================
    static void InstallInto(EnemyRig r, GameObject rigPrefab, Bounds b)
    {
        string path = $"{Prefabs}/{r.Prefab}.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.gameObject.layer = root.layer;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, visual);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;

            var so = new SerializedObject(root.GetComponent<Enemy>());
            so.FindProperty("visual").objectReferenceValue = visual;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 몸 콜라이더: 바닥(y=0)부터 모델 높이만큼 (최소 1m), 반지름은 기존 유지
            var cap = root.GetComponent<CapsuleCollider>();
            float h = Mathf.Max(1f, b.size.y);
            cap.height = h; cap.center = new Vector3(0f, h * 0.5f, 0f);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
