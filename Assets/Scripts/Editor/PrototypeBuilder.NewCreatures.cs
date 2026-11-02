using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 새 생명체 모델 임포트 (Meshy FBX: Z-up, 외부 텍스처):
///   마녀 엘리 = 몸(Elly_Body) + 지팡이(Elly_Staff) 따로,  새 소녀 유나 = 소녀(Yuna_Body) + 새(Yuna_Bird, 날갯짓 리깅) 따로.
/// 소녀 몸은 아직 리깅하지 않음 (나중에 Tools/Creatures/Auto Rig). 새는 몸/왼날개/오른날개 3뼈 스킨 + BirdFlight.
/// 미리보기 프리팹: Elly_Preview(지팡이를 든 위치), Yuna_Preview(새가 머리 위를 맴돎).
/// 메뉴: Roguelike/Import Elly and Yuna Models (재실행하면 프리팹 다시 생성)
/// </summary>
public static partial class PrototypeBuilder
{
    const string CreatureRoot = "Assets/Prototype/Creatures";

    [MenuItem("Roguelike/Import Elly and Yuna Models")]
    public static void ImportEllyAndYuna()
    {
        string elly = CreatureRoot + "/Elly", yuna = CreatureRoot + "/Yuna";
        var body = StaticModelPrefab($"{elly}/elly_body.fbx", $"{elly}/elly_body_basecolor.png", $"{elly}/Elly_Body.prefab", 0.9f, true, RemoveBroom);
        var staff = StaticModelPrefab($"{elly}/elly_staff.fbx", $"{elly}/elly_staff_basecolor.png", $"{elly}/Elly_Staff.prefab", 0.55f, false);
        var girl = StaticModelPrefab($"{yuna}/yuna_body.fbx", $"{yuna}/yuna_body_basecolor.png", $"{yuna}/Yuna_Body.prefab", 0.9f, true, RemoveHeadBird);
        var bird = RiggedBirdPrefab($"{yuna}/yuna_bird.fbx", $"{yuna}/yuna_bird_basecolor.png", $"{yuna}/Yuna_Bird.prefab", 0.32f);

        // 미리보기: 엘리 + 지팡이(오른손 쪽), 유나 + 새(머리 위를 맴돎)
        if (body && staff)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(body);
            var s = (GameObject)PrefabUtility.InstantiatePrefab(staff, root.transform);
            var hand = HandPoint(root);
            s.name = "Staff";
            s.transform.localPosition = hand + new Vector3(0f, 0.1f, 0f);
            s.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
            root.AddComponent<CreatureVisual>().Setup(null, null, 0.9f, 1.6f); // 리깅 전: 애니메이터 없음
            PrefabUtility.SaveAsPrefabAsset(root, $"{elly}/Elly_Preview.prefab");
            Object.DestroyImmediate(root);
        }
        if (girl && bird)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(girl);
            var b = (GameObject)PrefabUtility.InstantiatePrefab(bird, root.transform);
            b.name = "Bird";
            b.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            b.GetComponent<BirdFlight>().target = root.transform;
            root.AddComponent<CreatureVisual>().Setup(null, null, 0.9f, 1.6f);
            PrefabUtility.SaveAsPrefabAsset(root, $"{yuna}/Yuna_Preview.prefab");
            Object.DestroyImmediate(root);
        }
        // 생명체 목록에 등록 (로비에서 골라 전투에 데려갈 수 있게). 이름은 공격 증강 테이블의 생명체 이름과 맞춤
        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        RegisterCreature(cfg.ward, "마녀", "엘리 — 마법을 배우는 소녀. 지팡이로 마법을 쏨", $"{elly}/Elly_Preview.prefab");
        RegisterCreature(cfg.ward, "새 소녀", "유나 — 새를 좋아하는 소녀. 머리 위의 새가 함께 싸움", $"{yuna}/Yuna_Preview.prefab");
        EditorUtility.SetDirty(cfg.ward);
        AssetDatabase.SaveAssets();
        Debug.Log("[PrototypeBuilder] 엘리/유나 모델 임포트 완료");
    }

    /// <summary>WardConfig.creatures에 없으면 추가, 있으면 모델만 갱신 (다른 설정은 유지).</summary>
    static void RegisterCreature(WardConfig ward, string name, string desc, string modelPath)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var visual = model ? model.GetComponent<CreatureVisual>() : null;
        if (!visual) return;
        var list = new List<CreatureDef>(ward.creatures ?? new CreatureDef[0]);
        var def = list.Find(c => c != null && c.name == name);
        if (def == null) { def = new CreatureDef { name = name, desc = desc, tintStrength = 0.45f }; list.Add(def); }
        def.model = visual;
        ward.creatures = list.ToArray();
    }

    /// <summary>Meshy FBX 임포트 설정 + 매트 머티리얼 (로비 섬/당근과 같은 방식).</summary>
    static Material ImportMeshyFbx(string fbx, string tex)
    {
        var texImp = AssetImporter.GetAtPath(tex) as TextureImporter;
        if (texImp != null && texImp.maxTextureSize != 1024) { texImp.maxTextureSize = 1024; texImp.SaveAndReimport(); }
        string matPath = fbx.Replace(".fbx", "_Mat.mat");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true }; AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.12f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_EnvironmentReflections", 0f);
        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(mat);

        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        imp.bakeAxisConversion = true;
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.animationType = ModelImporterAnimationType.None;
        imp.importAnimation = false;
        imp.isReadable = true; // 새 리깅 / 손 위치 계산용
        imp.SaveAndReimport();
        return mat;
    }

    /// <summary>모델을 높이(가장 긴 변이 아니라 세로) h로 맞추고, standing이면 발바닥 y=0 · 중심 x,z=0, 아니면 중심을 원점에.</summary>
    /// <param name="deform">모델 공간(FBX 원본 기준) 정점 변형. 그림에 붙어 나온 부품(머리 위 새, 손의 빗자루)을 떼어 낼 때 사용.</param>
    static GameObject StaticModelPrefab(string fbx, string tex, string outPath, float h, bool standing, System.Func<Vector3, Vector3> deform = null)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (!src) { Debug.LogWarning("모델 없음: " + fbx); return null; }
        var mat = ImportMeshyFbx(fbx, tex);
        var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(outPath));
        var model = (GameObject)PrefabUtility.InstantiatePrefab(src);
        model.transform.SetParent(root.transform, false);
        foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        if (deform != null) DeformMeshes(model, deform, outPath.Replace(".prefab", "_Mesh.asset"));
        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation; // Meshy 모델 정면(-Z) → 게임 정면(+Z)
        var b = WorldBounds(model);
        float s = h / Mathf.Max(0.001f, standing ? b.size.y : Mathf.Max(b.size.x, b.size.y, b.size.z));
        model.transform.localScale *= s;
        b = WorldBounds(model);
        model.transform.position -= standing ? new Vector3(b.center.x, b.min.y, b.center.z) : b.center;
        var p = PrefabUtility.SaveAsPrefabAsset(root, outPath);
        Object.DestroyImmediate(root);
        return p;
    }

    /// <summary>모델의 메시를 복사해 정점을 변형한 메시 에셋으로 교체 (FBX는 그대로). 기존 에셋이 있으면 GUID 유지.</summary>
    static void DeformMeshes(GameObject model, System.Func<Vector3, Vector3> deform, string assetPath)
    {
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
        {
            var m = Object.Instantiate(mf.sharedMesh);
            m.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var toModel = mf.transform.localToWorldMatrix; // 모델은 아직 원점·회전 없음 → FBX 원본 좌표 (루트 스케일 포함)
            var back = toModel.inverse;
            var v = m.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = back.MultiplyPoint3x4(deform(toModel.MultiplyPoint3x4(v[i])));
            m.vertices = v;
            m.RecalculateNormals();
            m.RecalculateBounds();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing) { CopyMeshInto(m, existing); Object.DestroyImmediate(m); m = existing; EditorUtility.SetDirty(m); }
            else AssetDatabase.CreateAsset(m, assetPath);
            mf.sharedMesh = m;
            return; // Meshy 모델은 메시 하나
        }
    }

    /// <summary>유나: 머리 위에 붙어 나온 새를 지움 — cutY 위 정점을 머리 꼭대기 둥근 면으로 눌러 넣음.</summary>
    static Vector3 RemoveHeadBird(Vector3 p)
    {
        const float cutY = 0.62f;
        if (p.y <= cutY) return p;
        var c = new Vector3(0.0f, 0.15f, -0.02f); // 머리 중심 (FBX 원본 좌표)
        Vector3 d = p - c;
        d.x *= 0.35f; d.z *= 0.35f; // 옆으로 퍼진 새 정점은 정수리 가운데로 모음
        return c + d.normalized * (cutY - c.y);
    }

    /// <summary>엘리: 손에 붙어 나온 빗자루를 지움 — 몸 바깥 아래쪽 정점을 손 안쪽 한 점으로 모음 (삼각형이 사라짐).</summary>
    static Vector3 RemoveBroom(Vector3 p) => p.x < -0.45f && p.y < -0.45f ? new Vector3(-0.4f, -0.5f, 0f) : p;

    /// <summary>메시 에셋 내용을 통째로 교체 (CopySerialized는 정점 수가 다르면 제대로 안 바뀜).</summary>
    static void CopyMeshInto(Mesh src, Mesh dst)
    {
        dst.Clear();
        dst.indexFormat = src.indexFormat;
        dst.vertices = src.vertices;
        dst.normals = src.normals;
        dst.uv = src.uv;
        dst.subMeshCount = src.subMeshCount;
        for (int i = 0; i < src.subMeshCount; i++) dst.SetTriangles(src.GetTriangles(i), i);
        dst.boneWeights = src.boneWeights;
        dst.bindposes = src.bindposes;
        dst.RecalculateBounds();
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>오른손 근처 (프리팹 로컬): 몸 높이 20~55% 구간에서 x가 가장 큰 쪽 정점의 평균.</summary>
    static Vector3 HandPoint(GameObject root)
    {
        Vector3 best = new Vector3(0.3f, 0.45f, 0f);
        float maxX = float.MinValue;
        var pts = new List<Vector3>();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            var m = mf.sharedMesh; if (!m || !m.isReadable) continue;
            foreach (var v in m.vertices) { var w = root.transform.InverseTransformPoint(mf.transform.TransformPoint(v)); pts.Add(w); maxX = Mathf.Max(maxX, w.x); }
        }
        if (pts.Count == 0) return best;
        // 모자 챙(위쪽)은 빼고 몸 높이(20~55%)에서 가장 바깥쪽 = 손
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in pts) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
        float y0 = Mathf.Lerp(minY, maxY, 0.2f), y1 = Mathf.Lerp(minY, maxY, 0.55f);
        maxX = float.MinValue;
        foreach (var p in pts) if (p.y > y0 && p.y < y1) maxX = Mathf.Max(maxX, p.x);
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var p in pts) if (p.y > y0 && p.y < y1 && p.x > maxX - 0.04f) { sum += p; n++; }
        return n > 0 ? sum / n : best;
    }

    /// <summary>
    /// 새 모델 → 3뼈(몸/왼날개/오른날개) 스킨 메시. 양옆 끝(|x| 최대)의 78% 바깥 정점을 날개로 보고 날개 뼈에 부드럽게 붙임
    /// (몸 옆에 붙은 짧은 날개도 펄럭이게). 결과: 몸 중심이 원점, 앞 = +Z.
    /// </summary>
    static GameObject RiggedBirdPrefab(string fbx, string tex, string outPath, float size)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (!src) { Debug.LogWarning("모델 없음: " + fbx); return null; }
        var mat = ImportMeshyFbx(fbx, tex);

        // 모든 서브 메시를 하나로 합쳐 원점 기준 정점으로
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(src);
        var verts = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        foreach (var mf in tmp.GetComponentsInChildren<MeshFilter>())
        {
            var m = mf.sharedMesh; if (!m) continue;
            int off = verts.Count;
            // 이 새 모델은 누운 채로 나옴 → 세우고 위아래를 바로잡은 뒤 부리가 +Z(정면), 날개가 ±X(옆)가 되게 돌림
            var mtx = Matrix4x4.Rotate(Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(0f, 0f, 180f) * Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 180f, 0f)) * tmp.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            foreach (var v in m.vertices) verts.Add(mtx.MultiplyPoint3x4(v));
            foreach (var nn in m.normals) normals.Add(mtx.MultiplyVector(nn).normalized);
            var u = m.uv; for (int i = 0; i < m.vertexCount; i++) uvs.Add(i < u.Length ? u[i] : Vector2.zero);
            foreach (var t in m.triangles) tris.Add(t + off);
        }
        Object.DestroyImmediate(tmp);
        if (verts.Count == 0) return null;

        var b = new Bounds(verts[0], Vector3.zero);
        foreach (var v in verts) b.Encapsulate(v);
        float s = size / Mathf.Max(b.size.x, b.size.y, b.size.z);
        for (int i = 0; i < verts.Count; i++) verts[i] = (verts[i] - b.center) * s;

        // 날개 시작 폭: 양옆 끝(|x| 최대)의 78% 지점부터 날개 (둥근 몸 옆에 붙은 짧은 날개도 잡히게)
        float maxAbsX = 0.01f;
        foreach (var v in verts) maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(v.x));
        float bodyHalf = maxAbsX * 0.78f;
        // 날개 뿌리 높이: 몸 바깥 정점들의 평균 y
        float wingY = 0f; int wn = 0;
        foreach (var v in verts) if (Mathf.Abs(v.x) > bodyHalf * 1.1f) { wingY += v.y; wn++; }
        wingY = wn > 0 ? wingY / wn : 0f;

        var weights = new BoneWeight[verts.Count];
        for (int i = 0; i < verts.Count; i++)
        {
            float ax = Mathf.Abs(verts[i].x);
            float w = Mathf.Clamp01(Mathf.InverseLerp(bodyHalf * 0.95f, bodyHalf * 1.12f, ax));
            int wing = verts[i].x < 0f ? 1 : 2;
            weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f - w, boneIndex1 = wing, weight1 = w };
        }

        var mesh = new Mesh { name = "Yuna_Bird_Mesh", indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.boneWeights = weights;

        var root = new GameObject("Yuna_Bird");
        var bodyBone = new GameObject("Body").transform; bodyBone.SetParent(root.transform, false);
        var wingL = new GameObject("Wing_L").transform; wingL.SetParent(bodyBone, false); wingL.localPosition = new Vector3(-bodyHalf, wingY, 0f);
        var wingR = new GameObject("Wing_R").transform; wingR.SetParent(bodyBone, false); wingR.localPosition = new Vector3(bodyHalf, wingY, 0f);
        var bones = new[] { bodyBone, wingL, wingR };
        var binds = new Matrix4x4[3];
        for (int i = 0; i < 3; i++) binds[i] = bones[i].worldToLocalMatrix * root.transform.localToWorldMatrix;
        mesh.bindposes = binds;
        mesh.RecalculateBounds();
        string meshPath = outPath.Replace(".prefab", "_Mesh.asset");
        // 기존 메시 에셋이 있으면 내용만 교체 (GUID 유지 → 프리팹 참조가 끊기지 않게)
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing) { CopyMeshInto(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
        else AssetDatabase.CreateAsset(mesh, meshPath);

        var skin = new GameObject("Mesh").AddComponent<SkinnedMeshRenderer>();
        skin.transform.SetParent(root.transform, false);
        skin.sharedMesh = mesh; skin.bones = bones; skin.rootBone = bodyBone; skin.sharedMaterial = mat;
        skin.updateWhenOffscreen = true;
        var flight = root.AddComponent<BirdFlight>();
        flight.SetWings(wingL, wingR);
        var p = PrefabUtility.SaveAsPrefabAsset(root, outPath);
        Object.DestroyImmediate(root);
        return p;
    }
}
