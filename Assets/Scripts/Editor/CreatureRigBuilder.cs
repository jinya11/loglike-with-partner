using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 뼈대 없는 T자 자세 2족 모델(Meshy 등)을 자동 리깅 → 걷기/대기 모션 → 작은 생명체 모델 프리팹으로 만드는 도구.
/// 모델 형태(팔 높이, 어깨, 가랑이, 귀)를 정점 분포로 찾아 뼈를 배치하고, 부위별 규칙으로 가중치를 계산.
/// 결과물: Assets/Prototype/Creatures/{id}/ (Mesh, Material, Clip, Controller, Prefab)
/// 사용: 모델(FBX)을 선택하고 Tools/Creatures/Auto Rig Selected Model
/// </summary>
public static class CreatureRigBuilder
{
    public const string OutRoot = "Assets/Prototype/Creatures";

    public class Settings
    {
        public string id = "Creature";
        [Tooltip("최종 키 (월드 단위)")] public float height = 0.9f;
        public float smoothness = 0.25f;
        [Tooltip("걷기 한 주기 길이(초) / 이 이동 속도에서 애니메이션 속도 1")]
        public float walkCycle = 0.55f, walkRefSpeed = 1.6f;
        [Tooltip("팔을 내리는 각도 (T자 → 차렷)")] public float armDown = 72f;
        [Tooltip("머리카락 분리: 정수리 색과 비슷한 색의 정점은 팔/다리에 붙이지 않음 (팔 위를 덮는 긴 머리용)")]
        public bool separateHair = true;
        [Tooltip("머리카락 색 판정 허용 오차 (RGB 거리)")] public float hairColorTolerance = 0.12f;
    }

    // ---- 뼈 인덱스 ----
    enum B { Hips, Spine, Chest, Head, EarL, EarR, UpperArmL, LowerArmL, HandL, UpperArmR, LowerArmR, HandR, UpperLegL, LowerLegL, FootL, UpperLegR, LowerLegR, FootR, Count }
    static readonly int[] Parent = { -1, 0, 1, 2, 3, 3, 2, 6, 7, 2, 9, 10, 0, 12, 13, 0, 15, 16 };
    static readonly string[] Names = { "Hips", "Spine", "Chest", "Head", "Ear_L", "Ear_R", "UpperArm_L", "LowerArm_L", "Hand_L", "UpperArm_R", "LowerArm_R", "Hand_R", "UpperLeg_L", "LowerLeg_L", "Foot_L", "UpperLeg_R", "LowerLeg_R", "Foot_R" };

    /// <summary>분석된 체형 (모두 최종 크기 기준 월드 단위, 발바닥 y=0, 앞 = +Z).</summary>
    public struct Shape
    {
        public float maxX, armY, armR, armZ, armRz, shoulderX;
        public float crotchY, legX, legZ, legOuter, legRz;
        public float earBaseY, earX, topY, bodyZ, neckY, eyeY;
        public override string ToString() =>
            $"armY={armY:F3} armR={armR:F3} shoulderX={shoulderX:F3} maxX={maxX:F3} armZ={armZ:F3} crotchY={crotchY:F3} legX={legX:F3} legOuter={legOuter:F3} neckY={neckY:F3} earBaseY={earBaseY:F3} earX={earX:F3} top={topY:F3} bodyZ={bodyZ:F3}";
    }

    [MenuItem("Tools/Creatures/Auto Rig Selected Model")]
    static void BuildSelected()
    {
        var go = Selection.activeObject as GameObject;
        if (!go) { EditorUtility.DisplayDialog("Auto Rig", "프로젝트 창에서 모델(FBX)을 선택하세요.", "확인"); return; }
        var s = new Settings { id = go.name };
        var prefab = Build(go, s, out string log);
        Debug.Log("[CreatureRig] " + log);
        if (prefab) Selection.activeObject = prefab;
    }

    /// <summary>모델을 리깅해서 프리팹 생성. 원본 재질의 텍스처(베이스/노멀)를 사용.</summary>
    public static GameObject Build(GameObject source, Settings s, out string log)
    {
        var mf = source.GetComponentInChildren<MeshFilter>();
        var srcMesh = mf.sharedMesh;
        string dir = $"{OutRoot}/{s.id}";
        EnsureFolder(dir);

        // 1) 월드 기준으로 굽고 (FBX의 회전/배율 제거), 발바닥 원점 + 목표 키로 정규화
        Matrix4x4 m = mf.transform.localToWorldMatrix;
        var src = srcMesh.vertices;
        int n = src.Length;
        var v = new Vector3[n];
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < n; i++) { v[i] = m.MultiplyPoint3x4(src[i]); min = Vector3.Min(min, v[i]); max = Vector3.Max(max, v[i]); }
        float scale = s.height / (max.y - min.y);
        var offset = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
        for (int i = 0; i < n; i++) v[i] = (v[i] - offset) * scale;
        var nrm = srcMesh.normals; var tan = srcMesh.tangents;
        for (int i = 0; i < nrm.Length; i++) nrm[i] = m.MultiplyVector(nrm[i]).normalized;
        for (int i = 0; i < tan.Length; i++) { var t3 = m.MultiplyVector(tan[i]).normalized; tan[i] = new Vector4(t3.x, t3.y, t3.z, tan[i].w); }

        // 2) 체형 분석
        var sh = Analyze(v, srcMesh.triangles, s.height);

        // 3) 뼈 위치
        var pos = BonePositions(sh);

        // 4) 가중치 (머리카락 분리 옵션: 텍스처 색으로 머리카락 정점 판정)
        bool[] hair = s.separateHair ? HairMask(source, srcMesh.uv, v, sh, s) : null;
        var adj = Adjacency(v, srcMesh.triangles);
        if (hair != null) VoteMask(hair, adj, 2);
        var weights = ComputeWeights(v, sh, pos, s.height, hair, adj);

        // 5) 메시 저장
        var mesh = new Mesh { name = s.id + "_Mesh" };
        if (n > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = v; mesh.normals = nrm;
        if (tan.Length == n) mesh.tangents = tan;
        mesh.uv = srcMesh.uv;
        mesh.subMeshCount = srcMesh.subMeshCount;
        for (int k = 0; k < srcMesh.subMeshCount; k++) mesh.SetTriangles(srcMesh.GetTriangles(k), k);
        mesh.boneWeights = weights;
        var bind = new Matrix4x4[(int)B.Count];
        for (int b = 0; b < bind.Length; b++) bind[b] = Matrix4x4.Translate(-pos[b]); // 뼈 회전 없음 (항등)
        mesh.bindposes = bind;
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        mesh = SaveAsset(mesh, $"{dir}/{s.id}_Mesh.asset");

        // 6) 재질 (검은 점 원인인 금속 맵은 쓰지 않고, 반짝임은 낮게)
        var mat = BuildMaterial(source, mf, s, $"{dir}/{s.id}_Mat.mat");

        // 7) 모션 + 컨트롤러
        var idle = BuildClip(s, sh, pos, false, $"{dir}/{s.id}_Idle.anim");
        var walk = BuildClip(s, sh, pos, true, $"{dir}/{s.id}_Walk.anim");
        var ctrl = BuildController(idle, walk, $"{dir}/{s.id}_Animator.controller");

        // 8) 프리팹
        var root = new GameObject(s.id);
        var bones = new Transform[(int)B.Count];
        for (int b = 0; b < bones.Length; b++)
        {
            bones[b] = new GameObject(Names[b]).transform;
            bones[b].SetParent(Parent[b] < 0 ? root.transform : bones[Parent[b]], false);
            bones[b].position = pos[b];
        }
        var skinGo = new GameObject("Model");
        skinGo.transform.SetParent(root.transform, false);
        var smr = skinGo.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh; smr.bones = bones; smr.rootBone = bones[(int)B.Hips];
        smr.sharedMaterial = mat;
        smr.localBounds = new Bounds(new Vector3(0f, s.height * 0.5f - pos[(int)B.Hips].y, 0f), new Vector3(s.height * 1.2f, s.height * 1.3f, s.height * 1.2f));
        var eye = new GameObject("EyeAnchor").transform;
        eye.SetParent(bones[(int)B.Head], false);
        eye.position = new Vector3(0f, sh.eyeY, sh.bodyZ + s.height * 0.18f);
        var anim = root.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        var vis = root.AddComponent<CreatureVisual>();
        vis.Setup(anim, eye, s.height, s.walkRefSpeed);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{dir}/{s.id}.prefab");
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        log = $"{s.id}: v={n} shape: {sh}";
        return prefab;
    }

    // =====================================================================
    // 체형 분석
    // =====================================================================
    public static Shape Analyze(Vector3[] v, int[] tris, float H)
    {
        var sh = new Shape();
        int n = v.Length;
        float topY = 0f, maxX = 0f;
        for (int i = 0; i < n; i++) { topY = Mathf.Max(topY, v[i].y); maxX = Mathf.Max(maxX, Mathf.Abs(v[i].x)); }
        sh.topY = topY; sh.maxX = maxX;

        // 팔 높이/앞뒤 중심: 가장 바깥 15% 정점 (손/소매 끝)
        float tipX = maxX * 0.85f;
        float sy = 0f, sz = 0f; int c = 0;
        for (int i = 0; i < n; i++)
            if (Mathf.Abs(v[i].x) > tipX) { sy += v[i].y; sz += v[i].z; c++; }
        sh.armY = sy / c; sh.armZ = sz / c;
        // 팔 굵기: 손보다 안쪽(소매) 구간 [0.7, 0.85]·maxX 의 세로/앞뒤 반폭
        float yMin = 9f, yMax = -9f, zMin = 9f, zMax = -9f;
        for (int i = 0; i < n; i++)
        {
            float ax = Mathf.Abs(v[i].x);
            if (ax < maxX * 0.7f || ax > tipX || Mathf.Abs(v[i].y - sh.armY) > 0.12f * H) continue;
            yMin = Mathf.Min(yMin, v[i].y); yMax = Mathf.Max(yMax, v[i].y); zMin = Mathf.Min(zMin, v[i].z); zMax = Mathf.Max(zMax, v[i].z);
        }
        sh.armR = Mathf.Max(0.02f * H, (yMax - yMin) * 0.5f);
        sh.armRz = Mathf.Max(0.02f * H, (zMax - zMin) * 0.5f);

        // 어깨: 바깥에서 안쪽으로 훑으며, 팔 아래쪽으로 몸통이 이어지기 시작하는 지점
        // (위쪽은 머리카락이 팔 위를 덮는 경우가 많아 아래쪽만 봄. 뒤로 늘어진 머리는 앞뒤 범위로 제외)
        float step = 0.01f * H;
        sh.shoulderX = maxX * 0.5f;
        float zWin = Mathf.Max(sh.armRz * 1.3f, 0.04f * H);
        for (float x = maxX - step; x > 0.02f * H; x -= step)
        {
            float lo = 9f;
            for (int i = 0; i < n; i++)
            {
                float ax = Mathf.Abs(v[i].x);
                if (ax < x || ax >= x + step) continue;
                if (Mathf.Abs(v[i].z - sh.armZ) > zWin || v[i].y > sh.armY + sh.armR || v[i].y < sh.armY - 0.3f * H) continue;
                lo = Mathf.Min(lo, v[i].y);
            }
            if (lo < sh.armY - sh.armR * 2.5f) { sh.shoulderX = x + step; break; }
        }

        // 가랑이: 몸통 중간에서 아래로 훑으며 가운데 평면(x=0)을 지나는 면이 처음 없어지는 높이
        // (정점이 아니라 삼각형으로 판정해야 성긴 메시에서도 정확)
        sh.crotchY = 0.2f * H;
        for (float y = sh.armY - 0.1f * H; y > 0.02f * H; y -= step * 0.5f)
            if (!CrossesCenter(v, tris, y, y + step * 0.5f, sh.armZ, 0.06f * H)) { sh.crotchY = y + step * 0.5f; break; }
        // 다리 중심/바깥 폭/앞뒤 범위 (가랑이 아래 70%)
        var ax2 = new List<float>(); float lz = 0f; c = 0; float lzMin = 9f, lzMax = -9f;
        for (int i = 0; i < n; i++)
            if (v[i].y < sh.crotchY * 0.7f) { ax2.Add(Mathf.Abs(v[i].x)); lz += v[i].z; c++; lzMin = Mathf.Min(lzMin, v[i].z); lzMax = Mathf.Max(lzMax, v[i].z); }
        ax2.Sort();
        sh.legX = ax2.Count > 0 ? ax2[ax2.Count / 2] : 0.06f * H;
        sh.legOuter = ax2.Count > 0 ? ax2[(int)(ax2.Count * 0.97f)] : 0.12f * H;
        sh.legZ = c > 0 ? lz / c : 0f;
        sh.legRz = Mathf.Max(0.03f * H, (lzMax - lzMin) * 0.5f);

        // 귀: 위에서 아래로, 가운데(x≈0)에 처음 정점이 생기는 높이 = 머리 꼭대기 (그 위는 양쪽 귀)
        sh.earBaseY = topY;
        for (float y = topY - step * 0.5f; y > sh.armY; y -= step * 0.5f)
            if (CrossesCenter(v, tris, y, y + step * 0.5f, 0f, 9f)) { sh.earBaseY = y; break; }
        float ex = 0f; c = 0;
        for (int i = 0; i < n; i++) if (v[i].y > sh.earBaseY) { ex += Mathf.Abs(v[i].x); c++; }
        sh.earX = c > 0 ? ex / c : 0.08f * H;

        // 몸통 앞뒤 중심
        float bz = 0f; c = 0;
        for (int i = 0; i < n; i++) if (Mathf.Abs(v[i].x) < 0.05f * H && v[i].y > sh.crotchY && v[i].y < sh.armY) { bz += v[i].z; c++; }
        sh.bodyZ = c > 0 ? bz / c : 0f;

        sh.neckY = sh.armY + sh.armR * 0.9f;
        sh.eyeY = sh.neckY + (sh.earBaseY - sh.neckY) * 0.4f;
        return sh;
    }

    /// <summary>y 구간 [y0,y1]에서 x=0 평면을 가로지르는 삼각형이 있는지 (z     /// <summary>y 구간 [y0,y1]에서 x=0 평면을 가로지르는 삼각형이 있는지 (앞뒤 범위 |z-zc|&lt;zw).</summary>gt; zc-zw 인 것만).</summary>
    static bool CrossesCenter(Vector3[] v, int[] tris, float y0, float y1, float zc, float zw)
    {
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 a = v[tris[t]], b = v[tris[t + 1]], c = v[tris[t + 2]];
            float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            if (minX > 0f || maxX < 0f) continue;
            float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
            if (maxY < y0 || minY > y1) continue;
            float z = (a.z + b.z + c.z) / 3f;
            if (z > zc - zw) return true; // 뒤로 늘어진 머리카락만 제외 (앞면은 모두 포함)
        }
        return false;
    }

    static Vector3[] BonePositions(Shape sh)
    {
        var p = new Vector3[(int)B.Count];
        float torso = sh.armY - sh.crotchY;
        p[(int)B.Hips] = new Vector3(0f, sh.crotchY + torso * 0.15f, sh.bodyZ);
        p[(int)B.Spine] = new Vector3(0f, sh.crotchY + torso * 0.5f, sh.bodyZ);
        p[(int)B.Chest] = new Vector3(0f, sh.armY - sh.armR * 0.5f, sh.bodyZ);
        p[(int)B.Head] = new Vector3(0f, sh.neckY, sh.bodyZ);
        p[(int)B.EarL] = new Vector3(sh.earX, sh.earBaseY, sh.bodyZ);
        p[(int)B.EarR] = new Vector3(-sh.earX, sh.earBaseY, sh.bodyZ);
        float armLen = sh.maxX - sh.shoulderX;
        for (int side = 0; side < 2; side++)
        {
            float sx = side == 0 ? 1f : -1f;
            int ua = side == 0 ? (int)B.UpperArmL : (int)B.UpperArmR;
            p[ua] = new Vector3(sx * sh.shoulderX, sh.armY, sh.armZ);
            p[ua + 1] = new Vector3(sx * (sh.shoulderX + armLen * 0.45f), sh.armY, sh.armZ);
            p[ua + 2] = new Vector3(sx * (sh.shoulderX + armLen * 0.75f), sh.armY, sh.armZ);
            int ul = side == 0 ? (int)B.UpperLegL : (int)B.UpperLegR;
            p[ul] = new Vector3(sx * sh.legX, sh.crotchY, sh.legZ);
            p[ul + 1] = new Vector3(sx * sh.legX, sh.crotchY * 0.5f, sh.legZ);
            p[ul + 2] = new Vector3(sx * sh.legX, sh.crotchY * 0.15f, sh.legZ);
        }
        return p;
    }

    // =====================================================================
    // 가중치: 부위 판정(팔/다리/머리/귀) + 경계 혼합, 사슬 안에서는 길이 방향 보간
    // =====================================================================
    /// <summary>정수리(귀 사이 머리 꼭대기) 색과 비슷한 정점 = 머리카락.</summary>
    static bool[] HairMask(GameObject source, Vector2[] uv, Vector3[] v, Shape sh, Settings s)
    {
        var baseTex = FindTexture(source, "basecolor", "base_color", "albedo");
        if (!baseTex || uv == null || uv.Length != v.Length) return null;
        var img = new Texture2D(2, 2);
        img.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(baseTex)));
        int n = v.Length;
        var col = new Color[n];
        for (int i = 0; i < n; i++) col[i] = img.GetPixelBilinear(uv[i].x, uv[i].y);
        Color hairCol = Color.black; int c = 0;
        float crownY = sh.earBaseY - 0.04f * s.height;
        for (int i = 0; i < n; i++)
            if (v[i].y > crownY && v[i].y < sh.earBaseY + 0.01f * s.height && Mathf.Abs(v[i].x) < sh.earX * 0.5f) { hairCol += col[i]; c++; }
        Object.DestroyImmediate(img);
        if (c == 0) return null;
        hairCol /= c;
        // 밝은 옷(흰 소매 등)과 구분되도록 "색 기운(채도 방향)"과 밝기로 판정. 머리카락은 끝으로 갈수록 진해져도 같은 방향
        var hairChroma = new Vector2(hairCol.r - hairCol.g, hairCol.r - hairCol.b);
        float chromaLen = hairChroma.magnitude, hairLum = (hairCol.r + hairCol.g + hairCol.b) / 3f;
        var dir = chromaLen > 0f ? hairChroma / chromaLen : Vector2.zero;
        var mask = new bool[n];
        float tol = s.hairColorTolerance;
        for (int i = 0; i < n; i++)
        {
            if (v[i].y <= sh.crotchY) continue; // 머리카락은 가랑이 위에만
            var cc = col[i];
            float lum = (cc.r + cc.g + cc.b) / 3f;
            if (chromaLen < 0.05f) // 무채색 머리카락: 단순 색 거리
            {
                float dr = cc.r - hairCol.r, dg = cc.g - hairCol.g, db = cc.b - hairCol.b;
                mask[i] = dr * dr + dg * dg + db * db < tol * tol;
            }
            else
                mask[i] = Vector2.Dot(new Vector2(cc.r - cc.g, cc.r - cc.b), dir) > chromaLen * 0.7f && Mathf.Abs(lum - hairLum) < tol * 1.5f;
        }
        return mask;
    }

    static Texture2D FindTexture(GameObject source, params string[] keys)
    {
        string srcDir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(source)).Replace('\\', '/');
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { srcDir }))
        {
            string tp = AssetDatabase.GUIDToAssetPath(guid);
            string f = System.IO.Path.GetFileName(tp).ToLowerInvariant();
            foreach (var k in keys) if (f.Contains(k)) return AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
        }
        return null;
    }

    static BoneWeight[] ComputeWeights(Vector3[] v, Shape sh, Vector3[] p, float H, bool[] hair, int[][] adj)
    {
        int n = v.Length;
        var raw = new float[n][];
        var w = new float[(int)B.Count];
        float bw = 0.03f * H;
        float armLen = sh.maxX - sh.shoulderX;
        for (int i = 0; i < n; i++)
        {
            System.Array.Clear(w, 0, w.Length);
            Vector3 q = v[i];
            float ax = Mathf.Abs(q.x);
            int side = q.x >= 0f ? 0 : 1;

            // 팔 (팔 높이·앞뒤 범위 안 + 어깨 바깥)
            float arm = Smooth(sh.shoulderX - bw, sh.shoulderX + bw, ax)
                        * (1f - Smooth(sh.armR * 1.6f, sh.armR * 1.6f + bw, Mathf.Abs(q.y - sh.armY)))
                        * (1f - Smooth(sh.armRz * 1.6f, sh.armRz * 1.6f + bw, Mathf.Abs(q.z - sh.armZ)));
            // 다리 (가랑이 아래 + 다리 폭/앞뒤 범위 안)
            float leg = (1f - Smooth(sh.crotchY - bw, sh.crotchY + bw, q.y))
                        * (1f - Smooth(sh.legOuter, sh.legOuter + bw, ax))
                        * (1f - Smooth(sh.legRz * 1.3f, sh.legRz * 1.3f + bw, Mathf.Abs(q.z - sh.legZ)));
            // 머리카락은 팔다리를 따라가지 않음. 단, 손/소매 끝(피부색이 머리색과 비슷할 수 있음)은 제외:
            // 팔 위쪽·뒤쪽에 있고 손 쪽 40% 구간이 아닌 정점만 머리카락으로 취급
            if (hair != null && hair[i] && ax < sh.shoulderX + armLen * 0.6f && (q.y > sh.armY || q.z < sh.armZ - sh.armRz))
            { arm = 0f; leg = 0f; }
            // 머리 (목 위, 팔 제외)
            float head = Smooth(sh.neckY - bw, sh.neckY + bw, q.y) * (1f - arm);
            arm *= 1f - leg;
            float torso = Mathf.Max(0f, 1f - arm - leg - head);

            if (arm > 0f)
            {
                float t = Mathf.Clamp01((ax - sh.shoulderX) / armLen);
                int ua = side == 0 ? (int)B.UpperArmL : (int)B.UpperArmR;
                Chain3(w, ua, ua + 1, ua + 2, t, 0.45f, 0.75f, 0.08f, arm);
            }
            if (leg > 0f)
            {
                float t = 1f - Mathf.Clamp01(q.y / sh.crotchY); // 0=가랑이, 1=발바닥
                int ul = side == 0 ? (int)B.UpperLegL : (int)B.UpperLegR;
                Chain3(w, ul, ul + 1, ul + 2, t, 0.5f, 0.85f, 0.08f, leg);
            }
            if (head > 0f)
            {
                float ear = Smooth(sh.earBaseY - bw * 0.5f, sh.earBaseY + bw, q.y);
                w[(int)B.Head] += head * (1f - ear);
                w[side == 0 ? (int)B.EarL : (int)B.EarR] += head * ear;
            }
            if (torso > 0f)
            {
                float y = q.y;
                float h0 = p[(int)B.Hips].y, h1 = p[(int)B.Spine].y, h2 = p[(int)B.Chest].y;
                if (y <= h0) w[(int)B.Hips] += torso;
                else if (y <= h1) { float k = (y - h0) / (h1 - h0); w[(int)B.Hips] += torso * (1f - k); w[(int)B.Spine] += torso * k; }
                else if (y <= h2) { float k = (y - h1) / (h2 - h1); w[(int)B.Spine] += torso * (1f - k); w[(int)B.Chest] += torso * k; }
                else w[(int)B.Chest] += torso;
            }
            raw[i] = (float[])w.Clone();
        }
        // 이웃 정점과 가중치를 섞어 튀는 정점(가시)과 관절 꺾임을 부드럽게
        SmoothWeights(raw, adj, 3);
        var result = new BoneWeight[n];
        for (int i = 0; i < n; i++) result[i] = Top4(raw[i]);
        return result;
    }

    /// <summary>정점 이웃 (UV 이음새로 갈라진 같은 위치의 정점은 하나로 묶음).</summary>
    static int[][] Adjacency(Vector3[] v, int[] tris)
    {
        int n = v.Length;
        // 같은 위치 정점 묶기
        var weld = new int[n];
        var map = new Dictionary<Vector3Int, int>(n);
        for (int i = 0; i < n; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 1e4f), Mathf.RoundToInt(v[i].y * 1e4f), Mathf.RoundToInt(v[i].z * 1e4f));
            if (!map.TryGetValue(key, out int g)) { g = i; map[key] = i; }
            weld[i] = g;
        }
        var sets = new HashSet<int>[n];
        for (int t = 0; t < tris.Length; t += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = weld[tris[t + k]], b = weld[tris[t + (k + 1) % 3]];
                (sets[a] ??= new HashSet<int>()).Add(b);
                (sets[b] ??= new HashSet<int>()).Add(a);
            }
        var adj = new int[n][];
        for (int i = 0; i < n; i++)
        {
            var s = sets[weld[i]];
            var list = new List<int>();
            if (s != null) foreach (int g in s) list.Add(g);
            if (weld[i] != i) list.Add(weld[i]); // 묶인 대표 정점과도 연결
            adj[i] = list.ToArray();
        }
        // 대표가 아닌 정점의 값도 대표를 통해 이어지도록, 대표 쪽에도 묶인 정점 추가
        for (int i = 0; i < n; i++)
            if (weld[i] != i)
            {
                var a = adj[weld[i]];
                System.Array.Resize(ref a, a.Length + 1); a[a.Length - 1] = i; adj[weld[i]] = a;
            }
        return adj;
    }

    /// <summary>다수결로 마스크 다듬기 (주변 대부분과 다른 외톨이 정점 정리).</summary>
    static void VoteMask(bool[] mask, int[][] adj, int iterations)
    {
        var next = new bool[mask.Length];
        for (int it = 0; it < iterations; it++)
        {
            for (int i = 0; i < mask.Length; i++)
            {
                int on = mask[i] ? 1 : 0, total = 1;
                foreach (int j in adj[i]) { if (mask[j]) on++; total++; }
                next[i] = on * 2 > total;
            }
            System.Array.Copy(next, mask, mask.Length);
        }
    }

    static void SmoothWeights(float[][] w, int[][] adj, int iterations)
    {
        int n = w.Length, bc = w[0].Length;
        var next = new float[n][];
        for (int i = 0; i < n; i++) next[i] = new float[bc];
        for (int it = 0; it < iterations; it++)
        {
            for (int i = 0; i < n; i++)
            {
                var a = adj[i];
                var o = next[i];
                for (int b = 0; b < bc; b++) o[b] = w[i][b] * 0.5f;
                if (a.Length == 0) { for (int b = 0; b < bc; b++) o[b] = w[i][b]; continue; }
                float k = 0.5f / a.Length;
                foreach (int j in a) { var wj = w[j]; for (int b = 0; b < bc; b++) o[b] += wj[b] * k; }
            }
            for (int i = 0; i < n; i++) System.Array.Copy(next[i], w[i], bc);
        }
    }

    /// <summary>사슬(3개 뼈)에서 위치 t(0~1)에 따른 가중치. a,b = 경계, blend = 경계 혼합 폭.</summary>
    static void Chain3(float[] w, int b0, int b1, int b2, float t, float a, float b, float blend, float total)
    {
        float k1 = Smooth(a - blend, a + blend, t), k2 = Smooth(b - blend, b + blend, t);
        w[b0] += total * (1f - k1);
        w[b1] += total * (k1 - k2);
        w[b2] += total * k2;
    }

    static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

    static BoneWeight Top4(float[] w)
    {
        int i0 = -1, i1 = -1, i2 = -1, i3 = -1;
        for (int i = 0; i < w.Length; i++)
        {
            if (w[i] <= 0f) continue;
            if (i0 < 0 || w[i] > w[i0]) { i3 = i2; i2 = i1; i1 = i0; i0 = i; }
            else if (i1 < 0 || w[i] > w[i1]) { i3 = i2; i2 = i1; i1 = i; }
            else if (i2 < 0 || w[i] > w[i2]) { i3 = i2; i2 = i; }
            else if (i3 < 0 || w[i] > w[i3]) i3 = i;
        }
        var bw = new BoneWeight();
        float s = 0f;
        if (i0 >= 0) s += w[i0]; if (i1 >= 0) s += w[i1]; if (i2 >= 0) s += w[i2]; if (i3 >= 0) s += w[i3];
        if (s <= 0f) { bw.boneIndex0 = (int)B.Spine; bw.weight0 = 1f; return bw; }
        if (i0 >= 0) { bw.boneIndex0 = i0; bw.weight0 = w[i0] / s; }
        if (i1 >= 0) { bw.boneIndex1 = i1; bw.weight1 = w[i1] / s; }
        if (i2 >= 0) { bw.boneIndex2 = i2; bw.weight2 = w[i2] / s; }
        if (i3 >= 0) { bw.boneIndex3 = i3; bw.weight3 = w[i3] / s; }
        return bw;
    }

    // =====================================================================
    // 모션: 걷기(다리/팔 흔들기, 엉덩이 오르내림, 귀 출렁임) / 대기(숨쉬기, 귀 까딱)
    // =====================================================================
    static AnimationClip BuildClip(Settings s, Shape sh, Vector3[] p, bool walk, string path)
    {
        var clip = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(path), frameRate = 30 };
        float len = walk ? s.walkCycle : 2.4f;
        const int keys = 25;
        var rot = new Dictionary<int, Keyframe[][]>();
        var hipY = new Keyframe[keys];
        for (int b = 0; b < (int)B.Count; b++) rot[b] = new[] { new Keyframe[keys], new Keyframe[keys], new Keyframe[keys], new Keyframe[keys] };

        for (int k = 0; k < keys; k++)
        {
            float t = k / (float)(keys - 1);
            float time = t * len;
            float ph = t * Mathf.PI * 2f;
            var e = new Vector3[(int)B.Count]; // 오일러 (도)
            float bob;
            float down = s.armDown;
            if (walk)
            {
                float sw = Mathf.Sin(ph);
                // +X 회전 = 발이 뒤로. sw>0이면 왼다리가 뒤
                float thighL = 28f * sw, thighR = -28f * sw;
                // 다리가 앞으로 나가는 동안(들린 다리) 무릎 굽힘 (발이 뒤로 접힘)
                float kneeL = Mathf.Max(0f, -Mathf.Cos(ph)) * 40f, kneeR = Mathf.Max(0f, Mathf.Cos(ph)) * 40f;
                e[(int)B.UpperLegL] = new Vector3(thighL, 0f, 0f);
                e[(int)B.UpperLegR] = new Vector3(thighR, 0f, 0f);
                e[(int)B.LowerLegL] = new Vector3(kneeL, 0f, 0f);
                e[(int)B.LowerLegR] = new Vector3(kneeR, 0f, 0f);
                e[(int)B.FootL] = new Vector3(-(thighL + kneeL) * 0.6f, 0f, 0f); // 발바닥은 대체로 수평 유지
                e[(int)B.FootR] = new Vector3(-(thighR + kneeR) * 0.6f, 0f, 0f);
                e[(int)B.UpperArmL] = new Vector3(-22f * sw, 0f, -down);
                e[(int)B.UpperArmR] = new Vector3(22f * sw, 0f, down);
                e[(int)B.LowerArmL] = new Vector3(0f, -10f, 0f);
                e[(int)B.LowerArmR] = new Vector3(0f, 10f, 0f);
                e[(int)B.Hips] = new Vector3(0f, 6f * sw, 3f * Mathf.Cos(ph * 2f));
                e[(int)B.Spine] = new Vector3(4f, -3f * sw, 0f);
                e[(int)B.Chest] = new Vector3(2f, -4f * sw, 0f);
                e[(int)B.Head] = new Vector3(-2f + 2f * Mathf.Cos(ph * 2f), 4f * sw, 0f);
                float ear = 8f * Mathf.Sin(ph * 2f - 0.8f);
                e[(int)B.EarL] = new Vector3(-ear, 0f, -4f - ear * 0.5f);
                e[(int)B.EarR] = new Vector3(-ear, 0f, 4f + ear * 0.5f);
                bob = Mathf.Abs(Mathf.Cos(ph)) * 0.035f * s.height;
            }
            else
            {
                float br = Mathf.Sin(ph);
                e[(int)B.UpperArmL] = new Vector3(0f, 0f, -down + 2f * br);
                e[(int)B.UpperArmR] = new Vector3(0f, 0f, down - 2f * br);
                e[(int)B.LowerArmL] = new Vector3(0f, -12f, 0f);
                e[(int)B.LowerArmR] = new Vector3(0f, 12f, 0f);
                e[(int)B.Chest] = new Vector3(-1.5f * br, 0f, 0f);
                e[(int)B.Head] = new Vector3(1.5f * br, 0f, 2f * Mathf.Sin(ph * 0.5f));
                // 귀 까딱 (한 주기 중 한 번)
                float flick = Mathf.Max(0f, Mathf.Sin(ph * 2f - 1f)) * Mathf.Max(0f, Mathf.Sin(ph * 0.5f));
                e[(int)B.EarL] = new Vector3(-10f * flick, 0f, -3f);
                e[(int)B.EarR] = new Vector3(-4f * flick, 0f, 3f);
                bob = -0.006f * s.height * (0.5f + 0.5f * br);
            }
            for (int b = 0; b < (int)B.Count; b++)
            {
                var q = Quaternion.Euler(e[b]);
                rot[b][0][k] = new Keyframe(time, q.x); rot[b][1][k] = new Keyframe(time, q.y);
                rot[b][2][k] = new Keyframe(time, q.z); rot[b][3][k] = new Keyframe(time, q.w);
            }
            hipY[k] = new Keyframe(time, p[(int)B.Hips].y + bob);
        }

        for (int b = 0; b < (int)B.Count; b++)
        {
            string bp = BonePath(b);
            string[] comp = { "localRotation.x", "localRotation.y", "localRotation.z", "localRotation.w" };
            for (int c = 0; c < 4; c++) clip.SetCurve(bp, typeof(Transform), comp[c], Smoothed(rot[b][c]));
        }
        string hp = BonePath((int)B.Hips);
        clip.SetCurve(hp, typeof(Transform), "localPosition.x", AnimationCurve.Constant(0f, len, p[(int)B.Hips].x));
        clip.SetCurve(hp, typeof(Transform), "localPosition.y", Smoothed(hipY));
        clip.SetCurve(hp, typeof(Transform), "localPosition.z", AnimationCurve.Constant(0f, len, p[(int)B.Hips].z));
        clip.EnsureQuaternionContinuity();
        var st = AnimationUtility.GetAnimationClipSettings(clip);
        st.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, st);
        return SaveAsset(clip, path);
    }

    static AnimationCurve Smoothed(Keyframe[] k)
    {
        var c = new AnimationCurve(k);
        for (int i = 0; i < k.Length; i++) c.SmoothTangents(i, 0f);
        return c;
    }

    static string BonePath(int b)
    {
        string s = Names[b];
        for (int p = Parent[b]; p >= 0; p = Parent[p]) s = Names[p] + "/" + s;
        return s;
    }

    static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, string path)
    {
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter(CreatureVisual.SpeedParam, AnimatorControllerParameterType.Float);
        var state = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree);
        tree.blendParameter = CreatureVisual.SpeedParam;
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 1f);
        ctrl.layers[0].stateMachine.defaultState = state;
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static Material BuildMaterial(GameObject source, MeshFilter mf, Settings s, string path)
    {
        // Meshy 텍스처: 모델과 같은 폴더의 *basecolor*, *normal*
        string srcDir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(source)).Replace('\\', '/');
        Texture2D baseTex = null, normalTex = null;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { srcDir }))
        {
            string tp = AssetDatabase.GUIDToAssetPath(guid);
            string f = System.IO.Path.GetFileName(tp).ToLowerInvariant();
            if (f.Contains("basecolor") || f.Contains("base_color") || f.Contains("albedo")) baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            else if (f.Contains("normal")) normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.shaderKeywords = new string[0];
        mat.SetColor("_BaseColor", Color.white);
        if (baseTex) { mat.SetTexture("_BaseMap", baseTex); mat.SetTexture("_MainTex", baseTex); }
        if (normalTex) { mat.SetTexture("_BumpMap", normalTex); mat.SetFloat("_BumpScale", 0.6f); mat.EnableKeyword("_NORMALMAP"); }
        mat.SetTexture("_MetallicGlossMap", null);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", s.smoothness);
        mat.SetFloat("_SmoothnessTextureChannel", 0f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>검수용: 프리팹을 클립의 여러 시점으로 포즈를 잡아 정면/측면 렌더 (가로로 이어 붙인 PNG).</summary>
    public static void RenderPreview(GameObject prefab, AnimationClip clip, float[] times, string pngPath, bool side = false)
    {
        var go = Object.Instantiate(prefab);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.position = new Vector3(2000f, 0f, 2000f);
        var camGo = new GameObject("PreviewCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.5f, 0.55f, 0.6f); cam.fieldOfView = 30f;
        var lightGo = new GameObject("PreviewLight") { hideFlags = HideFlags.HideAndDontSave };
        var li = lightGo.AddComponent<Light>(); li.type = LightType.Directional; lightGo.transform.rotation = Quaternion.Euler(35f, 160f, 0f);
        const int S = 384;
        var rt = new RenderTexture(S, S, 24); cam.targetTexture = rt;
        var tex = new Texture2D(S * times.Length, S, TextureFormat.RGB24, false);
        var vis = go.GetComponent<CreatureVisual>();
        float h = vis ? vis.Height : 1f;
        Vector3 c = go.transform.position + Vector3.up * h * 0.5f;
        Vector3 dir = side ? new Vector3(1f, 0.25f, 0.15f) : new Vector3(0.15f, 0.25f, 1f);
        for (int i = 0; i < times.Length; i++)
        {
            if (clip) clip.SampleAnimation(go, times[i]);
            cam.transform.position = c + dir.normalized * h * 2.6f; cam.transform.LookAt(c);
            cam.Render();
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, S, S), i * S, 0);
        }
        tex.Apply(); RenderTexture.active = null;
        System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
        Object.DestroyImmediate(go); Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo); rt.Release();
    }

    static T SaveAsset<T>(T obj, string path) where T : Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing)
        {
            EditorUtility.CopySerialized(obj, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(obj, path);
        return obj;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
