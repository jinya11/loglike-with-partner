using UnityEngine;

/// <summary>
/// 로비에서 생명체가 자유롭게 걸어다님: 주변 임의 지점으로 걸어가고 잠깐 쉬기를 반복. 바닥은 아래로 레이를 쏴서 찾고,
/// 앞이 막히거나 바닥이 끊기면(섬 가장자리) 다른 곳을 고름.
/// 리깅된 모델(CreatureVisual, 예: 토끼)은 걷기 모션 속도를 넘겨주고, 리깅 안 된 모델은 통통 튀는 걸음(위아래 + 좌우 흔들림)으로 표현.
/// </summary>
public class LobbyWanderer : MonoBehaviour
{
    [Tooltip("돌아다니는 중심 (비우면 처음 위치)")] public Vector3 home;
    public float radius = 9f, speed = 1.4f, turnSpeed = 360f;
    public Vector2 pauseRange = new Vector2(1f, 3.5f);
    [Tooltip("플레이어가 이 거리 안이면 멈춰서 바라봄")] public float stopNearPlayer = 2.4f;
    [Tooltip("리깅 안 된 모델의 걸음 표현: 튀는 높이 / 좌우 기울기 / 걸음 빈도")]
    public float hopHeight = 0.06f, sway = 7f, stepRate = 7f;
    [Tooltip("통통 튀는 걸음을 적용할 모델 (비우면 첫 번째 자식)")] public Transform visual;

    CreatureVisual rigged;
    Vector3 target;
    float pause, stepPhase, ground;
    bool moving;
    static readonly RaycastHit[] hits = new RaycastHit[8];

    void Start()
    {
        if (home == Vector3.zero) home = transform.position;
        rigged = GetComponentInChildren<CreatureVisual>();
        if (rigged && !rigged.Animated) rigged = null; // 리깅 전 모델은 통통 튀는 걸음으로
        if (!visual && transform.childCount > 0) visual = transform.GetChild(0);
        ground = Ground(transform.position, out _) ?? transform.position.y;
        pause = Random.Range(0f, pauseRange.y);
    }

    /// <summary>p 아래 바닥 높이 (자기 자신/생명체끼리는 무시). 없으면 null.</summary>
    float? Ground(Vector3 p, out Vector3 normal)
    {
        normal = Vector3.up;
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 4f, Vector3.down, hits, 12f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MinValue; bool found = false;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].transform.IsChildOf(transform) || hits[i].collider.GetComponentInParent<LobbyWanderer>()) continue;
            if (hits[i].point.y > best) { best = hits[i].point.y; normal = hits[i].normal; found = true; }
        }
        return found ? best : (float?)null;
    }

    bool PickTarget()
    {
        for (int i = 0; i < 12; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            var p = new Vector3(home.x + r.x, transform.position.y, home.z + r.y);
            var g = Ground(p, out var nrm);
            // 평평하고 지금 바닥과 높이가 비슷한 곳만 (계단/제단 위, 섬 밖 제외)
            if (g == null || nrm.y < 0.8f || Mathf.Abs(g.Value - ground) > 0.4f) continue;
            p.y = g.Value;
            if (Blocked(transform.position, p)) continue;
            target = p;
            return true;
        }
        return false;
    }

    bool Blocked(Vector3 from, Vector3 to)
    {
        Vector3 a = from + Vector3.up * 0.45f, d = to - from; d.y = 0f;
        float len = d.magnitude;
        if (len < 0.01f) return false;
        int n = Physics.SphereCastNonAlloc(a, 0.25f, d / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c.transform.IsChildOf(transform) || c.GetComponentInParent<LobbyWanderer>() || c.GetComponentInParent<PlayerController>()) continue;
            if (hits[i].normal.y > 0.8f) continue; // 바닥
            return true;
        }
        return false;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        float speedNow = 0f;
        // 플레이어가 가까이 오면 멈춰서 바라봄 (말 걸기/고르기 쉽게)
        var pc = PlayerController.I;
        Vector3 toPlayer = pc ? pc.Position - transform.position : Vector3.one * 99f; toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < stopNearPlayer * stopNearPlayer)
        {
            moving = false; pause = Mathf.Max(pause, 1f);
            if (toPlayer.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), turnSpeed * dt);
        }
        else if (!moving)
        {
            if ((pause -= dt) <= 0f)
            {
                moving = PickTarget();
                if (!moving) pause = 0.5f; // 갈 곳을 못 찾으면 잠시 뒤 다시
            }
        }
        else
        {
            Vector3 pos = transform.position;
            Vector3 to = target - pos; to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.15f) { moving = false; pause = Random.Range(pauseRange.x, pauseRange.y); }
            else
            {
                Vector3 dir = to / dist;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * dt);
                Vector3 next = pos + dir * Mathf.Min(dist, speed * dt);
                var g = Ground(next, out _);
                // 바닥이 끊기거나(섬 가장자리) 갑자기 높아지면 멈추고 다른 곳으로
                if (g == null || Mathf.Abs(g.Value - ground) > 0.4f) { moving = false; pause = 0.3f; }
                else { ground = g.Value; next.y = ground; transform.position = next; speedNow = speed; }
            }
        }

        if (rigged) { rigged.SetMoveSpeed(speedNow, dt); return; }
        if (!visual) return;
        // 리깅 없는 모델: 통통 튀는 걸음
        if (speedNow > 0f) stepPhase += dt * stepRate;
        else stepPhase = Mathf.MoveTowards(stepPhase, Mathf.Round(stepPhase / Mathf.PI) * Mathf.PI, dt * stepRate); // 멈출 때 바른 자세로
        float s = Mathf.Sin(stepPhase);
        visual.localPosition = new Vector3(0f, Mathf.Abs(s) * hopHeight, 0f);
        visual.localRotation = Quaternion.Euler(0f, 0f, s * sway);
    }
}
