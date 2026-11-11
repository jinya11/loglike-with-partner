using UnityEngine;

/// <summary>
/// 숲 파생형 "자연 농지"의 규칙 컴포넌트 (전용 씬 Region_Forest_Farmland에 배치).
/// 배치는 모두 씬 오브젝트: 평평한 논 지형(전용 TerrainData), 논 구획(RicePaddy, 에디터에서도 벼가 그려짐), 논두렁/물 댄 논바닥(지형 레이어), 벼가루 파티클.
/// 규칙: 벼는 허리 높이라 적은 아래쪽이 묻혀 윗부분만 보임(실제 깊이 가림) / 폭격하면 그 범위의 벼가 베여 적이 드러남.
/// 적 구성은 StageVariantDef.enemyBias (감속형 증가). 수치는 VariantConfig.farmland.
/// </summary>
public class ForestFarmlandVariant : MonoBehaviour
{
    StageDirector stage;
    FarmlandSettings s;
    int cutCount;

    public int CutCount => cutCount;

    public void Init(StageDirector st, FarmlandSettings settings)
    {
        stage = st; s = settings;
        // 지형 높이를 따라 벼를 다시 심음 (플레이 중 Ground 기준)
        foreach (var p in RicePaddy.All) p.Rebuild();
        CompanionBase.Bombarded += OnBombard;
        if (HudExtras.I) HudExtras.I.SetStatus("벼에 가려 적은 윗부분만 보입니다 — 폭격(R)으로 벼를 베면 드러납니다");
    }

    void OnDestroy()
    {
        CompanionBase.Bombarded -= OnBombard;
        if (HudExtras.I) HudExtras.I.SetStatus(null);
    }

    void OnBombard(Vector3 center, float radius)
    {
        int cut = RicePaddy.CutAll(center, radius * (s != null ? s.cutRadiusMul : 1f));
        if (cut == 0) return;
        cutCount += cut;
        Vfx.Pickup(center + Vector3.up, s != null ? s.riceTipColor : new Color(0.9f, 0.82f, 0.4f), 20);
    }
}
