using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 지역 씬(심연 제외)에 Unity Terrain을 배치. 기본으로 완만한 언덕/구릉 하이트맵 + 풀/바위 레이어(경사 기준)를 채움.
/// 이후에는 Terrain 툴(Raise/Lower, Smooth, Paint 등)로 직접 땅을 올리고 내려서 편집.
/// - 메뉴 Roguelike/Setup Region Terrains: Terrain이 없는 씬에만 추가. TerrainData 에셋이 이미 있으면 그대로 다시 씀 (손으로 고친 지형 유지).
/// - 메뉴 Roguelike/Setup Region Terrains (언덕 다시 생성): 확인 후 모든 지역 지형의 하이트맵을 새로 생성 (손으로 고친 것 덮어씀).
/// TerrainData: Assets/Prototype/Terrain/{씬}_Terrain.asset. 바닥(Arena/Floor)은 보이지 않게 하고 충돌만 남김.
/// </summary>
public static partial class PrototypeBuilder
{
    const string TerrainDir = Root + "/Terrain";
    const float TerrainSize = 300f, TerrainHeight = 24f;

    [MenuItem("Roguelike/Setup Region Terrains")]
    public static void SetupRegionTerrains() => SetupTerrains(false);

    [MenuItem("Roguelike/Setup Region Terrains (언덕 다시 생성)")]
    public static void RegenerateRegionTerrains()
    {
        if (!EditorUtility.DisplayDialog("지형 다시 생성", "모든 지역(심연 제외) 지형의 언덕을 새로 생성합니다. Terrain 툴로 직접 고친 내용은 덮어씁니다. 계속할까요?", "다시 생성", "취소")) return;
        SetupTerrains(true);
    }

    static void SetupTerrains(bool regenerate)
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty && !EditorUtility.DisplayDialog("지형 설정", $"'{active.name}' 씬에 저장하지 않은 변경이 있습니다. 씬을 닫고 계속할까요? (변경은 저장되지 않습니다)", "계속", "취소"))
            return;
        string returnTo = active.path;
        if (!AssetDatabase.IsValidFolder(TerrainDir)) AssetDatabase.CreateFolder(Root, "Terrain");

        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        var st = cfg.stage;
        var scenes = new List<string>();
        if (st.regionPool != null) foreach (var r in st.regionPool) if (r != null && !string.IsNullOrEmpty(r.scene)) scenes.Add(r.scene);
        if (st.centerRegion != null && !string.IsNullOrEmpty(st.centerRegion.scene)) scenes.Add(st.centerRegion.scene);
        var hills = st.ground.hills;

        int done = 0;
        foreach (var name in scenes)
        {
            if (st.IsExpandable(name)) continue; // 심연은 평지 (보더 확장 구조)
            string path = $"{RegionDir}/{name}.unity";
            if (!System.IO.File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var existing = Object.FindAnyObjectByType<Terrain>();
            if (existing && !regenerate) continue;

            var floor = GameObject.Find("Arena/Floor");
            Color floorColor = new Color(0.3f, 0.42f, 0.25f);
            if (floor && floor.TryGetComponent(out Renderer fr))
            {
                if (fr.sharedMaterial && fr.sharedMaterial.HasColor("_BaseColor")) floorColor = fr.sharedMaterial.GetColor("_BaseColor");
                fr.enabled = false; // 지형이 바닥을 대신 (충돌은 남김)
            }

            string dataPath = $"{TerrainDir}/{name}_Terrain.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            bool fresh = td == null;
            if (fresh)
            {
                td = new TerrainData { heightmapResolution = 513, alphamapResolution = 256, baseMapResolution = 512 };
                td.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
                AssetDatabase.CreateAsset(td, dataPath);
            }
            td.terrainLayers = new[] { Layer(name + "_Grass", floorColor, 0.06f), Layer(name + "_Rock", Color.Lerp(new Color(0.45f, 0.4f, 0.36f), floorColor, 0.25f), 0.12f) };
            var pos = new Vector3(-TerrainSize * 0.5f, 0f, -TerrainSize * 0.5f);
            if (fresh || regenerate) TerrainHills.Generate(td, pos, hills, name.GetHashCode());
            else TerrainHills.PaintLayers(td, hills.rockSlope);
            EditorUtility.SetDirty(td);

            GameObject go = existing ? existing.gameObject : Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            var arena = GameObject.Find("Arena");
            if (arena) go.transform.SetParent(arena.transform, true);
            go.transform.position = pos;
            var t = go.GetComponent<Terrain>();
            t.terrainData = td;
            if (go.TryGetComponent(out TerrainCollider tc)) tc.terrainData = td;
            t.materialTemplate = TerrainMaterial();
            t.drawInstanced = true;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            t.heightmapPixelError = 3f;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            done++;
        }
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo);
        Debug.Log($"[PrototypeBuilder] 지역 지형(Terrain) {done}개 설정{(regenerate ? " (언덕 다시 생성)" : "")} — Terrain 툴로 직접 편집 가능");
    }

    /// <summary>단색 + 약한 얼룩 텍스처의 TerrainLayer (지역 바닥 색 기준).</summary>
    static TerrainLayer Layer(string name, Color c, float noise)
    {
        string texPath = $"{TerrainDir}/{name}_Tex.asset", layerPath = $"{TerrainDir}/{name}.terrainlayer";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (!tex)
        {
            tex = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat };
            var px = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float n = (Mathf.PerlinNoise(x * 0.15f, y * 0.15f) - 0.5f) * 2f * noise + (Random.value - 0.5f) * noise * 0.6f;
                    px[y * 64 + x] = new Color(c.r * (1f + n), c.g * (1f + n), c.b * (1f + n), 1f);
                }
            tex.SetPixels(px); tex.Apply(true);
            AssetDatabase.CreateAsset(tex, texPath);
        }
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (!layer)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, layerPath);
        }
        layer.diffuseTexture = tex;
        layer.tileSize = new Vector2(8f, 8f);
        layer.smoothness = 0.05f;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    static Material TerrainMaterial()
    {
        string path = $"{TerrainDir}/TerrainLit.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        var sh = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (!sh) return null; // URP가 아니면 기본 지형 재질
        m = new Material(sh) { name = "TerrainLit" };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
