using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>설원 파생 씬: 얼어붙은 호수 (평평한 호수 지형 + 얼음판 + 스페이스 연타 UI).</summary>
public static partial class PrototypeBuilder
{
    static void BuildFrozenLake(GameObject root, FrozenLakeSettings s, string sceneName)
    {
        var rule = root.AddComponent<FrozenLakeVariant>();

        // 지형: 기본형 설원 지형을 복사한 전용 TerrainData — 호수 자리만 평평하게 (둘레는 원래 언덕으로 부드럽게 이어짐)
        var t = Terrain.activeTerrain;
        if (t && t.terrainData)
        {
            string src = AssetDatabase.GetAssetPath(t.terrainData);
            string dst = $"{TerrainDir}/{sceneName}_Terrain.asset";
            AssetDatabase.DeleteAsset(dst);
            AssetDatabase.CopyAsset(src, dst);
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dst);
            FlattenLake(td, t.transform.position, s.lakeRadius + 1.5f, 10f);
            EditorUtility.SetDirty(td);
            t.terrainData = td;
            if (t.TryGetComponent(out TerrainCollider tc)) tc.terrainData = td;
        }

        // 얼음판 (칸마다 프리팹 하나, 일부는 처음부터 금 감)
        var prefab = IceTilePrefab(s);
        var tiles = new GameObject("IceTiles").transform;
        tiles.SetParent(root.transform, false);
        var rng = new System.Random(5);
        int n = Mathf.CeilToInt(s.lakeRadius / s.tileSize);
        for (int iz = -n; iz <= n; iz++)
            for (int ix = -n; ix <= n; ix++)
            {
                var p = new Vector3(ix * s.tileSize, 0f, iz * s.tileSize);
                if (p.magnitude > s.lakeRadius - s.tileSize * 0.5f) continue;
                p.y = SampleGround(t, p.x, p.z);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, tiles);
                go.name = $"Ice_{ix}_{iz}";
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, rng.Next(4) * 90f, 0f));
                var tile = go.GetComponent<IceTile>();
                tile.cracked = p.magnitude > s.safeRadius && rng.NextDouble() < s.crackedRatio;
                if (tile.cracks) tile.cracks.SetActive(tile.cracked);
            }

        // 스페이스 연타 UI
        var ui = (GameObject)PrefabUtility.InstantiatePrefab(IceEscapePrefab(), root.transform);
        rule.escapeUI = ui.GetComponent<SpaceMashUI>();
    }

    /// <summary>중심에서 radius 안은 중심 높이로 평평하게, 그 바깥 blend(m)는 원래 높이로 부드럽게.</summary>
    static void FlattenLake(TerrainData td, Vector3 tpos, float radius, float blend)
    {
        int res = td.heightmapResolution;
        var h = td.GetHeights(0, 0, res, res);
        int cx = Mathf.RoundToInt((-tpos.x) / td.size.x * (res - 1)), cz = Mathf.RoundToInt((-tpos.z) / td.size.z * (res - 1));
        float level = h[Mathf.Clamp(cz, 0, res - 1), Mathf.Clamp(cx, 0, res - 1)];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float wx = tpos.x + x / (float)(res - 1) * td.size.x, wz = tpos.z + z / (float)(res - 1) * td.size.z;
                float d = Mathf.Sqrt(wx * wx + wz * wz);
                if (d >= radius + blend) continue;
                float k = d <= radius ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (d - radius) / blend);
                h[z, x] = Mathf.Lerp(h[z, x], level, k);
            }
        td.SetHeights(0, 0, h);
    }

    /// <summary>얼음판 한 칸: 얼음 몸체 + 균열(어두운 선) + 구멍일 때 보이는 물. 충돌 없음 (높이는 지형).</summary>
    static GameObject IceTilePrefab(FrozenLakeSettings s)
    {
        string path = $"{VariantPrefabDir}/IceTile.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var iceMat = ClayMat("Ice", s.iceColor);
        var crackMat = ClayMat("IceCrack", s.crackColor);
        var waterMat = ClayMat("LakeWater", s.waterColor);
        waterMat.SetFloat("_Smoothness", 0.55f); // 물만 살짝 반짝
        float sz = s.tileSize;
        var root = new GameObject("IceTile");
        var tile = root.AddComponent<IceTile>();
        var ice = new GameObject("Ice").transform;
        ice.SetParent(root.transform, false);
        LPart(PrimitiveType.Cube, "Slab", ice, new Vector3(0f, -0.1f, 0f), new Vector3(sz - 0.1f, 0.3f, sz - 0.1f), iceMat);
        tile.ice = ice;
        var cracks = new GameObject("Cracks");
        cracks.transform.SetParent(ice, false);
        // 가운데에서 뻗어나가는 균열 (지그재그)
        void Crack(float x, float z, float len, float yaw) => LPart(PrimitiveType.Cube, "Crack", cracks.transform, new Vector3(x, 0.055f, z), new Vector3(0.07f, 0.02f, len), crackMat).transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Crack(-0.2f, 0.25f, 0.9f, 25f); Crack(-0.45f, 0.85f, 0.7f, -15f);
        Crack(0.3f, -0.1f, 0.8f, 110f); Crack(0.85f, 0.1f, 0.6f, 70f);
        Crack(-0.1f, -0.45f, 0.7f, 160f); Crack(-0.35f, -0.95f, 0.6f, 200f);
        Crack(-0.55f, 0f, 0.6f, 95f);
        tile.cracks = cracks;
        var water = LPart(PrimitiveType.Cube, "Water", root.transform, new Vector3(0f, -0.04f, 0f), new Vector3(sz - 0.1f, 0.04f, sz - 0.1f), waterMat).gameObject;
        // 구멍 가장자리 얼음 조각 (물과 함께 보임)
        LPart(PrimitiveType.Cube, "Rim_N", water.transform, new Vector3(0f, 1.5f, 0.45f), new Vector3(0.9f, 2.5f, 0.12f), iceMat);
        LPart(PrimitiveType.Cube, "Rim_E", water.transform, new Vector3(0.44f, 1.5f, -0.2f), new Vector3(0.14f, 2.5f, 0.5f), iceMat);
        water.SetActive(false);
        tile.water = water;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }

    /// <summary>스페이스 연타 UI: 화면 중앙에 스페이스 키 + 탈출 게이지 + 남은 시간 막대.</summary>
    static GameObject IceEscapePrefab()
    {
        string path = $"{VariantPrefabDir}/IceEscape.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var white = UiSprite("White", 8, (x, y) => 1f);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Paperlogy-7Bold SDF.asset");

        var root = new GameObject("IceEscape", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 11;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
        var ui = root.AddComponent<SpaceMashUI>();
        var content = UiRect("Content", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        ui.group = content.gameObject.AddComponent<CanvasGroup>(); ui.group.blocksRaycasts = false; ui.group.interactable = false;

        var c = new Vector2(0.5f, 0.5f);
        var panel = UiRect("Panel", content, c, c, new Vector2(0f, 150f), new Vector2(520f, 200f));
        // 스페이스 키
        var key = UiRect("Keycap", panel, c, c, new Vector2(0f, 40f), new Vector2(340f, 86f));
        var shadow = UiImage("KeyShadow", key, white, Vector2.zero, Vector2.one, new Vector2(0f, -8f), Vector2.zero);
        shadow.color = new Color(0.2f, 0.32f, 0.45f, 0.9f);
        ui.keyBack = UiImage("KeyBack", key, white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        ui.keyBack.gameObject.AddComponent<Outline>().effectColor = new Color(0.15f, 0.25f, 0.35f, 0.9f);
        var label = UiText("KeyLabel", key, font, 40, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        label.text = "SPACE"; label.color = new Color(0.2f, 0.32f, 0.45f); label.outlineWidth = 0f;
        ui.keycap = key;
        // 탈출 게이지
        var back = UiImage("ProgressBack", panel, white, c, c, new Vector2(0f, -38f), new Vector2(420f, 26f));
        back.color = new Color(0f, 0f, 0f, 0.45f);
        ui.progress = UiImage("Progress", panel, white, c, c, new Vector2(0f, -38f), new Vector2(414f, 20f));
        ui.progress.type = Image.Type.Filled; ui.progress.fillMethod = Image.FillMethod.Horizontal; ui.progress.fillOrigin = 0;
        // 남은 시간
        var tback = UiImage("TimeBack", panel, white, c, c, new Vector2(0f, -66f), new Vector2(420f, 10f));
        tback.color = new Color(0f, 0f, 0f, 0.45f);
        ui.timeBar = UiImage("Time", panel, white, c, c, new Vector2(0f, -66f), new Vector2(416f, 6f));
        ui.timeBar.type = Image.Type.Filled; ui.timeBar.fillMethod = Image.FillMethod.Horizontal; ui.timeBar.fillOrigin = 0;
        ui.counter = UiText("Counter", panel, font, 26, c, c, new Vector2(250f, -38f), new Vector2(90f, 34f));
        ui.counter.alignment = TextAlignmentOptions.Left;

        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }
}
