using UnityEngine;

/// <summary>
/// 묘지 파생형 "깨어난 묘지"의 규칙 컴포넌트 (전용 씬 Region_Graveyard_Awakened에 배치).
/// 배치는 모두 씬 오브젝트: 무덤(GraveMound 프리팹, 부술 수 있는 StageTarget).
/// 규칙:
///  - 부서지지 않은 무덤에서 적이 계속 일어남 (무덤마다 간격, 살아 있는 적이 많으면 쉼). 일반 스폰은 그대로.
///  - 클리어 조건: 처치 목표가 기존의 killTargetMul배 + 무덤 gravesToBreak개 파괴 (둘 다 채워야 포털).
///  - 무덤은 생명체 자동 공격의 대상이 되고(가장 가까운 것), 탄환/범위 공격/폭격으로 부서짐. 무덤 파괴는 처치 수에 안 침.
///  - HUD 정보 줄에 "무덤 부순 수/N" (수치만).
/// 수치는 VariantConfig.awakened.
/// </summary>
public class AwakenedGraveyardVariant : MonoBehaviour
{
    StageDirector stage;
    AwakenedGraveyardSettings s;
    int broken;

    public int BrokenCount => broken;
    public int Goal => Mathf.Min(s.gravesToBreak, GraveMound.All.Count);

    public void Init(StageDirector st, AwakenedGraveyardSettings settings)
    {
        stage = st; s = settings;
        float hp = st.EnemyHp * s.graveHpMul;
        foreach (var g in GraveMound.All)
        {
            g.transform.position = Ground.Snap(g.transform.position);
            g.Target.Init(hp);
            g.Target.Broken += _ => OnBroken(g);
            g.riseTimer = s.riseInterval * 0.5f + Random.Range(0f, s.firstRiseJitter);
        }
        st.MulKillTarget(s.killTargetMul);
        st.TargetShare = s.graveTargetShare; // 적에 둘러싸인 무덤도 노리게
        st.ExtraClearCondition = () => broken >= Goal;
        st.ExtraObjective = () => $"무덤 {broken}/{Goal}";
    }

    void OnDestroy()
    {
        if (stage) { stage.ExtraClearCondition = null; stage.ExtraObjective = null; stage.TargetShare = 0f; }
    }

    void OnBroken(GraveMound g)
    {
        broken++;
        Vector3 p = g.transform.position;
        Vfx.Death(p + Vector3.up * 0.4f, s.stoneColor);
        Vfx.Pickup(p + Vector3.up * 0.3f, s.dustColor, 18);
        g.ShowBroken();
        stage.CheckClear(); // 처치 목표를 이미 채웠으면 바로 포털
    }

    void Update()
    {
        if (s == null || GameManager.I.State != GameState.Playing || stage.PortalOpen) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        foreach (var g in GraveMound.All)
        {
            if (g.Broken || (g.riseTimer -= dt) > 0f) continue;
            g.riseTimer = s.riseInterval;
            int n = Random.Range(s.risePerWave.x, Mathf.Max(s.risePerWave.x, s.risePerWave.y) + 1);
            for (int i = 0; i < n && Enemy.Active.Count < s.riseCap; i++) Rise(g);
        }
    }

    /// <summary>무덤 둘레 흙에서 적 하나가 솟아오름 (땅속에서 튀어나옴 + 흙 튐 + 흙더미 자국).</summary>
    void Rise(GraveMound g)
    {
        Vector2 o = Random.insideUnitCircle.normalized * Random.Range(s.riseRadius * 0.5f, s.riseRadius);
        Vector3 p = g.transform.position + new Vector3(o.x, 0f, o.y);
        p = Arena.Clamp(p, 1.5f);
        if (stage.UseFlow && !stage.Nav.IsFree(p)) p = stage.Nav.NearestFree(p);
        var e = stage.SpawnEnemyAt(p);
        if (!e) return;
        e.Emerge(s.riseDuration, s.riseDepth);
        Vector3 at = Ground.Snap(p);
        Vfx.Pickup(at + Vector3.up * 0.2f, s.dustColor, 14); // 흙 튐
        Vfx.Death(at + Vector3.up * 0.1f, s.dustColor);
        stage.SpawnFx(at + Vector3.up * 0.03f, new Vector3(0.4f, 0.05f, 0.4f), new Vector3(2.2f, 0.05f, 2.2f), s.riseDuration, s.moundColor); // 갈라지는 흙 자국
    }
}
