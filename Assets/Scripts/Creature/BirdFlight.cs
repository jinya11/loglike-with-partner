using UnityEngine;

/// <summary>
/// 새 소녀(유나)의 새: 날갯짓하며 소녀(target) 머리 위 주변을 날아다님. 능력은 이 새를 통해 쓸 예정 (공격 시 AttackFrom 위치 사용).
/// 날개 뼈(wingL/wingR)는 BirdRig(에디터 메뉴)가 만든 프리팹에 연결됨. 날개는 앞뒤 축(로컬 Z) 기준으로 위아래로 펄럭임.
/// </summary>
public class BirdFlight : MonoBehaviour
{
    [Tooltip("따라다닐 대상 (비우면 Ward.I)")] public Transform target;
    [SerializeField] Transform wingL, wingR;
    [Header("비행")]
    [Tooltip("대상 발 기준 높이 (머리 위)")] public float height = 1.25f;
    [Tooltip("대상 주변을 도는 반경 (최소~최대, 천천히 변함)")] public Vector2 orbitRadius = new Vector2(0.4f, 1.1f);
    [Tooltip("한 바퀴 도는 데 걸리는 시간 (초)")] public float orbitPeriod = 3.2f;
    [Tooltip("위아래 출렁임")] public float bob = 0.18f;
    public float followSharpness = 6f, turnSpeed = 540f;
    [Header("날갯짓")]
    public float flapSpeed = 14f;
    [Tooltip("날개가 위아래로 움직이는 각도")] public float flapAngle = 50f;
    [Tooltip("날개 기본 각도 (+ 위로)")] public float flapBias = 10f;

    float phase, seed;
    Vector3 lastPos;
    Quaternion wingLBase, wingRBase;

    /// <summary>새가 공격을 날릴 위치.</summary>
    public Vector3 AttackFrom => transform.position;

    void Start()
    {
        seed = Random.value * 100f;
        if (wingL) wingLBase = wingL.localRotation;
        if (wingR) wingRBase = wingR.localRotation;
        lastPos = transform.position;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        var t = target ? target : (Ward.I ? Ward.I.transform : null);
        float time = Time.time;

        if (t)
        {
            // 반경이 천천히 바뀌는 원 궤도 + 출렁임 → 소녀 주변을 맴돌며 날아다님
            phase += dt * Mathf.PI * 2f / Mathf.Max(0.1f, orbitPeriod);
            float r = Mathf.Lerp(orbitRadius.x, orbitRadius.y, Mathf.PerlinNoise(seed, time * 0.35f));
            Vector3 goal = t.position + new Vector3(Mathf.Cos(phase) * r, height + Mathf.Sin(time * 2.3f + seed) * bob, Mathf.Sin(phase) * r);
            transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-followSharpness * dt));
        }

        // 이동 방향을 바라봄 (거의 안 움직이면 유지)
        Vector3 v = (transform.position - lastPos) / dt; lastPos = transform.position;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        if (flat.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat) * Quaternion.Euler(Mathf.Clamp(-v.y * 8f, -25f, 25f), 0f, 0f), turnSpeed * dt);

        // 날갯짓: 빨리 움직일수록 조금 더 빠르게
        float flap = Mathf.Sin(time * flapSpeed * (1f + Mathf.Clamp01(flat.magnitude * 0.15f))) * flapAngle + flapBias;
        if (wingL) wingL.localRotation = wingLBase * Quaternion.Euler(0f, 0f, -flap);
        if (wingR) wingR.localRotation = wingRBase * Quaternion.Euler(0f, 0f, flap);
    }

    public void SetWings(Transform left, Transform right) { wingL = left; wingR = right; }
}
