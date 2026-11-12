using UnityEngine;

/// <summary>
/// 묘지 파생형 "달빛 묘역"의 규칙 컴포넌트 (전용 씬 Region_Graveyard_Moonlit에 배치).
/// 배치는 모두 씬 오브젝트: 밤 조명, 달빛 구역(MoonBeam 프리팹).
/// 규칙:
///  - 달빛 구역이 천천히 옮겨 다님 (스테이지 안 걸을 수 있는 곳 사이를 오감, 목적지에서 잠깐 머묾).
///  - 달빛 안에서 작은 생명체는 체력을 회복.
///  - 작은 생명체의 기본 행동: 적이 가까우면 도망치되 달빛 쪽으로, 위협이 없으면 달빛을 따라감 (배회 대신).
///    플레이어의 명령(따라오기/정지/직접 조작)이 우선.
/// 수치는 VariantConfig.moonlit.
/// </summary>
public class MoonlitGraveyardVariant : MonoBehaviour
{
    StageDirector stage;
    MoonlitGraveyardSettings s;
    float sparkle;

    public void Init(StageDirector st, MoonlitGraveyardSettings settings)
    {
        stage = st; s = settings;
        foreach (var m in MoonBeam.All)
        {
            m.Fit(s.radius);
            m.PlaceAt(m.transform.position);
            m.target = PickTarget(m.transform.position);
        }
        Ward.Attractor = Nearest;
        Ward.AttractorStop = s.radius * s.stopFrac;
        Ward.AttractorFleeWeight = s.fleeBias;
    }

    void OnDestroy() { Ward.Attractor = null; Ward.AttractorStop = 2f; Ward.AttractorFleeWeight = 0.02f; }

    /// <summary>생명체에게 가장 가까운 달빛 중심.</summary>
    Vector3 Nearest()
    {
        Vector3 w = Ward.I != null ? Ward.I.Position : Vector3.zero;
        Vector3 best = w; float bestSq = float.MaxValue;
        foreach (var m in MoonBeam.All)
        {
            Vector3 d = m.transform.position - w; d.y = 0f;
            if (d.sqrMagnitude < bestSq) { bestSq = d.sqrMagnitude; best = m.transform.position; }
        }
        return best;
    }

    /// <summary>생명체가 달빛 안인지.</summary>
    public bool WardInMoon()
    {
        if (Ward.I == null) return false;
        Vector3 w = Ward.I.Position;
        foreach (var m in MoonBeam.All)
        {
            Vector3 d = m.transform.position - w; d.y = 0f;
            if (d.sqrMagnitude <= s.radius * s.radius) return true;
        }
        return false;
    }

    Vector3 PickTarget(Vector3 from)
    {
        Vector3 best = from; float bestSq = -1f;
        for (int i = 0; i < 24; i++)
        {
            Vector3 p = Arena.RandomPoint(0.8f);
            if (stage.Nav != null) p = stage.Nav.NearestFree(p);
            if (!Arena.Contains(p, s.radius * 0.6f)) continue;
            Vector3 d = p - from; d.y = 0f;
            if (d.sqrMagnitude >= s.minHop * s.minHop) return p;
            if (d.sqrMagnitude > bestSq) { bestSq = d.sqrMagnitude; best = p; }
        }
        return best;
    }

    void Update()
    {
        if (s == null || GameManager.I.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        foreach (var m in MoonBeam.All)
        {
            if (m.wait > 0f) { m.wait -= dt; continue; }
            Vector3 p = m.transform.position, d = m.target - p; d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.2f) { m.wait = s.pause; m.target = PickTarget(p); continue; }
            m.PlaceAt(p + d / dist * Mathf.Min(dist, s.moveSpeed * dt));
        }
        // 달빛 안에서 생명체 회복 (작은 반짝임)
        var ward = Ward.I;
        if (ward != null && ward.Alive && WardInMoon())
        {
            ward.Heal(s.healPerSec * dt);
            if ((sparkle -= dt) <= 0f) { sparkle = 0.5f; Vfx.Pickup(ward.Position + Vector3.up * 0.8f, s.moonColor, 3); }
        }
    }
}
