using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

// 로비 발판(IslandPlatform) 밑에 레퍼런스 그림처럼 아래로 뾰족해지는 바위 절벽(역원뿔)을 ProBuilder로 만든다.
// 발판 밑면 테두리 정점에서 시작해 층층이 좁아지며, 층마다 반지름·높이를 흔들어 각진 바위 결을 낸다.
public static partial class LobbyIslandBuilder
{
    const string UndersideName = "IslandUnderside";

    [MenuItem("Tools/Lobby Island/Build ProBuilder Underside")]
    public static void BuildUndersideMenu() => BuildUnderside(null, 7);

    public static string BuildUnderside(string capturePath, int seed)
    {
        var prevActive = SceneManager.GetActiveScene();
        var lobby = SceneManager.GetSceneByPath(LobbyPath);
        bool openedHere = false;
        if (!lobby.isLoaded)
        {
            lobby = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Additive);
            openedHere = true;
        }

        ProBuilderMesh platform = null;
        Camera mainCam = null;
        foreach (var go in lobby.GetRootGameObjects())
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "IslandPlatform") platform = t.GetComponent<ProBuilderMesh>();
                if (t.name == UndersideName) Object.DestroyImmediate(t.gameObject);
            }
            if (go.CompareTag("MainCamera")) mainCam = go.GetComponent<Camera>();
        }
        if (platform == null) return "IslandPlatform 없음";

        var rand = new System.Random(seed);
        float Rnd(float a, float b) => a + (float)rand.NextDouble() * (b - a);

        // 발판 밑면 테두리(가장 낮은 정점들)를 각도 순으로
        var world = platform.positions.Select(p => platform.transform.TransformPoint(p)).ToList();
        float bottom = world.Min(p => p.y);
        var center = new Vector3(world.Average(p => p.x), bottom, world.Average(p => p.z));
        var rim = world.Where(p => Mathf.Abs(p.y - bottom) < 0.01f && new Vector2(p.x - center.x, p.z - center.z).sqrMagnitude > 1f)
                       .GroupBy(p => Mathf.Round(Mathf.Atan2(p.z - center.z, p.x - center.x) * 1000f))
                       .Select(g => g.First())
                       .OrderBy(p => Mathf.Atan2(p.z - center.z, p.x - center.x))
                       .ToList();
        int sides = rim.Count;

        // 층별 (깊이 비율, 반지름 비율). 위쪽은 완만하게, 중간에 툭 튀어나온 바위 턱, 아래로 갈수록 급하게 좁아짐
        float depth = Mathf.Max(PlatformA, PlatformB) * 1.35f;
        var rings = new (float t, float s)[]
        {
            (0.00f, 1.00f), (0.05f, 0.97f), (0.12f, 0.99f), (0.20f, 0.88f), (0.28f, 0.84f),
            (0.36f, 0.86f), (0.46f, 0.70f), (0.56f, 0.60f), (0.66f, 0.47f), (0.76f, 0.34f),
            (0.86f, 0.21f), (0.94f, 0.10f),
        };
        var tip = center + new Vector3(Rnd(-3f, 3f), -depth, Rnd(-3f, 3f));

        var verts = new List<Vector3>();
        for (int r = 0; r < rings.Length; r++)
        {
            var (t, s) = rings[r];
            // 층 전체를 살짝 기울이고 중심을 끝점 쪽으로 옮겨 자연스러운 비대칭
            var ringCenter = Vector3.Lerp(center, new Vector3(tip.x, center.y, tip.z), t);
            for (int i = 0; i < sides; i++)
            {
                var offset = rim[i] - center;
                offset.y = 0f;
                float jag = r == 0 ? 0f : Rnd(-0.07f, 0.07f) * (1f - t * 0.5f);
                float dy = r == 0 ? 0f : Rnd(-1.6f, 1.6f);
                var p = ringCenter + offset * (s + jag);
                p.y = center.y - depth * t + dy;
                verts.Add(p);
            }
        }
        int tipIndex = verts.Count;
        verts.Add(tip);

        var rock = Lit("Lobby_Rock", new Color(0.76f, 0.72f, 0.86f));
        var rockLight = Lit("Lobby_RockLight", new Color(0.84f, 0.8f, 0.92f));
        var moss = Lit("Lobby_Moss", new Color(0.62f, 0.74f, 0.58f));

        var faces = new List<Face>();
        var faceMats = new List<Material>();
        for (int r = 0; r < rings.Length - 1; r++)
            for (int i = 0; i < sides; i++)
            {
                int a = r * sides + i, b = r * sides + (i + 1) % sides;
                int c = (r + 1) * sides + i, d = (r + 1) * sides + (i + 1) % sides;
                // 바깥을 향하도록 감는 순서(아래로 내려가는 링, 각도 증가 방향)
                faces.Add(new Face(new[] { a, c, b, b, c, d }));
                // 맨 위 띠는 이끼/덩굴 느낌의 초록, 그 아래는 층마다 밝기를 번갈아 바위 결
                faceMats.Add(r == 0 ? moss : (r % 2 == 0 ? rockLight : rock));
            }
        int last = (rings.Length - 1) * sides;
        for (int i = 0; i < sides; i++)
        {
            faces.Add(new Face(new[] { last + i, tipIndex, last + (i + 1) % sides }));
            faceMats.Add(rock);
        }

        var pb = ProBuilderMesh.Create(verts, faces);
        pb.gameObject.name = UndersideName;
        pb.transform.SetParent(platform.transform.parent, true);
        foreach (var m in faceMats.Distinct())
            pb.SetMaterial(pb.faces.Where((f, idx) => faceMats[idx] == m).ToList(), m);
        foreach (var f in pb.faces) f.smoothingGroup = 0; // 각진 로우폴리 면
        pb.ToMesh();
        pb.Refresh();
        // 바깥쪽을 향하지 않는 면이 있으면 노멀 기준으로 뒤집는다
        var flip = new List<Face>();
        foreach (var f in pb.faces)
        {
            var n = UnityEngine.ProBuilder.Math.Normal(pb, f);
            var fc = f.distinctIndexes.Select(i => pb.positions[i]).Aggregate(Vector3.zero, (acc, v) => acc + v) / f.distinctIndexes.Count;
            var outward = fc - pb.transform.InverseTransformPoint(new Vector3(center.x, fc.y, center.z));
            if (Vector3.Dot(n, outward) < 0f) flip.Add(f);
        }
        foreach (var f in flip) f.Reverse();
        pb.ToMesh();
        pb.Refresh();
        pb.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        EditorSceneManager.MarkSceneDirty(lobby);
        if (openedHere) EditorSceneManager.SaveScene(lobby);

        string result = $"underside built: sides={sides} flipped={flip.Count}";
        if (!string.IsNullOrEmpty(capturePath))
        {
            SceneManager.SetActiveScene(lobby);
            var wide = new GameObject("WideShot").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(wide.gameObject, lobby);
            wide.fieldOfView = 45f;
            wide.farClipPlane = 800f;
            wide.transform.position = new Vector3(18f, 20f, -135f);
            wide.transform.LookAt(new Vector3(18f, -22f, 6f));
            wide.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            result += "; " + Capture(wide, capturePath);
            Object.DestroyImmediate(wide.gameObject);
        }

        if (prevActive.IsValid() && prevActive.isLoaded) SceneManager.SetActiveScene(prevActive);
        if (openedHere) EditorSceneManager.CloseScene(lobby, true);
        return result;
    }
}
