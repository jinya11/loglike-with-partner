using UnityEditor;
using UnityEngine;

/// <summary>화산 파생 씬: 칼데라 (지형·보더 없음, 용암 호수를 둘러싼 고리 모양 플랫폼 + 오르내리는 용암 면).</summary>
public static partial class PrototypeBuilder
{
    static void BuildCaldera(GameObject root, CalderaSettings s)
    {
        var rule = root.AddComponent<CalderaVariant>();

        // 지형/보더 없음: Terrain과 보더 벽을 지우고, 경계는 원형(고리 바깥 반경)으로
        foreach (var t in Object.FindObjectsByType<Terrain>()) Object.DestroyImmediate(t.gameObject);
        var border = GameObject.Find("Arena/Border");
        if (border) Object.DestroyImmediate(border);
        var gen = Object.FindAnyObjectByType<StageGenerator>();
        if (gen)
        {
            var so = new SerializedObject(gen);
            so.FindProperty("border").objectReferenceValue = null;
            so.FindProperty("radius").floatValue = s.outerRadius + 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        // 감시자(플레이어)는 고리 위에서 시작
        float mid = (s.innerRadius + s.outerRadius) * 0.5f;
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player) { var p = player.transform.position; player.transform.position = new Vector3(0f, p.y, -mid); }

        // 용암 면 (넓게 — 고리 바깥도 용암)
        var lavaMat = ClayMat("CalderaLava", s.lavaColor);
        lavaMat.EnableKeyword("_EMISSION");
        lavaMat.SetColor("_EmissionColor", s.lavaColor * s.lavaGlow);
        lavaMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(lavaMat);
        var lava = new GameObject("Lava").transform;
        lava.SetParent(root.transform, false);
        lava.position = new Vector3(0f, s.highLevel, 0f);
        var sheet = LPart(PrimitiveType.Cylinder, "LavaSheet", lava, new Vector3(0f, -0.05f, 0f), new Vector3(s.outerRadius * 5f, 0.05f, s.outerRadius * 5f), lavaMat);
        sheet.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rule.lava = lava;
        // 용암 빛 (아래에서 올라오는 주황빛)
        var glow = new GameObject("LavaGlow").AddComponent<Light>();
        glow.transform.SetParent(lava, false); glow.transform.localPosition = new Vector3(0f, 2f, 0f);
        glow.type = LightType.Point; glow.range = s.innerRadius * 2.4f; glow.intensity = 3f; glow.color = s.lavaColor; glow.shadows = LightShadows.None;

        // 플랫폼 (칸마다 프리팹 하나). 고리 안쪽·바깥 가장자리 띠는 수위가 바뀔 때 가라앉음 (경계는 들쭉날쭉)
        var prefab = CalderaPlatformPrefab(s);
        var tiles = new GameObject("Platforms").transform;
        tiles.SetParent(root.transform, false);
        var rng = new System.Random(11);
        int n = Mathf.CeilToInt(s.outerRadius / s.tileSize);
        for (int iz = -n; iz <= n; iz++)
            for (int ix = -n; ix <= n; ix++)
            {
                var p = new Vector3(ix * s.tileSize, 0f, iz * s.tileSize);
                float r = p.magnitude;
                if (r < s.innerRadius || r > s.outerRadius) continue;
                float j = (Mathf.PerlinNoise(ix * 0.35f + 3.1f, iz * 0.35f + 7.7f) - 0.5f) * 2f * s.bandJitter;
                bool vanish = r < s.innerRadius + s.innerBand + j || r > s.outerRadius - s.outerBand + j;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, tiles);
                go.name = $"Platform_{ix}_{iz}";
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, rng.Next(4) * 90f, 0f));
                var plat = go.GetComponent<CalderaPlatform>();
                plat.SetVanishes(vanish); // 런타임 무작위 고르기(randomVanish)를 끄면 이 지정 그대로
            }
    }

    /// <summary>플랫폼 한 칸: 현무암 기둥(윗면 0, 아래로 길게) + 가라앉는 칸만 보이는 빛나는 틈. 충돌 없음 (높이는 평지 0).</summary>
    static GameObject CalderaPlatformPrefab(CalderaSettings s)
    {
        string path = $"{VariantPrefabDir}/CalderaPlatform.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var rock = ClayMat("CalderaRock", s.rockColor);
        var veinMat = VMat("CalderaVein", "Universal Render Pipeline/Unlit", s.lavaColor * 1.4f);
        float sz = s.tileSize;
        var root = new GameObject("CalderaPlatform");
        var plat = root.AddComponent<CalderaPlatform>();
        var body = new GameObject("Body").transform;
        body.SetParent(root.transform, false);
        plat.column = LPart(PrimitiveType.Cube, "Column", body, new Vector3(0f, -4f, 0f), new Vector3(sz - 0.12f, 8f, sz - 0.12f), rock);
        plat.rockMaterial = rock; plat.crustMaterial = ClayMat("CalderaCrust", s.crustColor);
        plat.body = body;
        var veins = new GameObject("Veins");
        veins.transform.SetParent(body, false);
        void Vein(float x, float z, float len, float yaw) => LPart(PrimitiveType.Cube, "Vein", veins.transform, new Vector3(x, 0.01f, z), new Vector3(0.09f, 0.03f, len), veinMat).transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Vein(-0.3f, 0.3f, 1.1f, 30f); Vein(0.4f, -0.2f, 0.9f, 115f); Vein(-0.2f, -0.6f, 0.8f, 170f); Vein(0.7f, 0.55f, 0.6f, 60f); Vein(-0.8f, -0.1f, 0.7f, 95f);
        veins.SetActive(false);
        plat.veins = veins;
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }
}
