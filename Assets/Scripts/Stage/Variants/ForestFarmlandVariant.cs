using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 숲 파생형 "자연 농지"의 규칙 컴포넌트 (전용 씬 Region_Forest_Farmland에 배치).
/// 배치는 모두 씬 오브젝트: 평평한 논 지형(전용 TerrainData), 논 구획(RicePaddy, 에디터에서도 벼가 그려짐), 논두렁/물 댄 논바닥(지형 레이어), 벼가루 파티클.
/// 규칙: 베이지 않은 벼 속의 적은 몸 일부만 보임 (게임 카메라가 내려다봐도 — 알파 컷아웃으로 조각조각 그림, 비율은 VariantConfig.farmland.hiddenFraction).
///       논두렁이나 폭격으로 벤 자리에서는 온전히 보임. 폭격하면 그 범위의 벼가 베임.
/// 적 구성은 StageVariantDef.enemyBias (감속형 증가).
/// </summary>
public class ForestFarmlandVariant : MonoBehaviour
{
    StageDirector stage;
    FarmlandSettings s;
    int cutCount;
    float tick;
    Texture2D maskTex;
    readonly Dictionary<Material, Material> maskMats = new Dictionary<Material, Material>(16);
    System.Func<Material, Material> mapper;

    public int CutCount => cutCount;

    public void Init(StageDirector st, FarmlandSettings settings)
    {
        stage = st; s = settings;
        // 지형 높이를 따라 벼를 다시 심음 (플레이 중 Ground 기준)
        foreach (var p in RicePaddy.All) p.Rebuild();
        CompanionBase.Bombarded += OnBombard;
        maskTex = MakeMaskTexture();
        mapper = MaskedMaterial;
    }

    void OnDestroy()
    {
        CompanionBase.Bombarded -= OnBombard;
        foreach (var e in Enemy.Active) e.SetMask(null);
        foreach (var m in maskMats.Values) if (m) Destroy(m);
        if (maskTex) Destroy(maskTex);
    }

    /// <summary>조각조각 알파 마스크 (얼룩 노이즈). 알파가 컷오프보다 낮은 곳은 안 그려짐 → 몸 일부만 보임.</summary>
    static Texture2D MakeMaskTexture()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "RiceMask", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        float ox = Random.value * 50f, oy = Random.value * 50f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // 세로로 긴 잎 모양 얼룩 (벼 잎 사이로 보이는 느낌)
                float a = Mathf.PerlinNoise(ox + x * 0.35f, oy + y * 0.08f) * 0.7f + Random.value * 0.3f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }

    /// <summary>적 원래 재질 → 같은 색의 알파 컷아웃 재질 (재질마다 한 번만 만들어 공유).</summary>
    Material MaskedMaterial(Material src)
    {
        if (maskMats.TryGetValue(src, out var m)) return m;
        m = new Material(src) { name = src.name + "_InRice" };
        if (m.HasTexture("_BaseMap")) { m.SetTexture("_BaseMap", maskTex); m.SetTextureScale("_BaseMap", new Vector2(s.maskTiling, s.maskTiling)); }
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", Mathf.Clamp01(s.hiddenFraction));
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        maskMats[src] = m;
        return m;
    }

    void Update()
    {
        if (s == null || (tick -= Time.deltaTime) > 0f) return;
        tick = 0.1f;
        foreach (var e in Enemy.Active)
            e.SetMask(RicePaddy.AnyCovers(e.transform.position) ? mapper : null);
    }

    void OnBombard(Vector3 center, float radius)
    {
        int cut = RicePaddy.CutAll(center, radius * s.cutRadiusMul);
        if (cut == 0) return;
        cutCount += cut;
        Vfx.Pickup(center + Vector3.up, s.riceTipColor, 20);
    }
}
