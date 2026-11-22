using UnityEngine;

/// <summary>
/// 날개 뼈(Wing_L/R) 날갯짓. 두 날개가 각자 타이머로 주기/세기/쉬는 시간을 매번 조금씩 다르게 뽑아
/// 서로 어긋나게 퍼덕임 (겹칠 때도, 엇갈릴 때도 있음). 이동 중엔 세게 자주, 대기 중엔 가끔 살랑.
/// CreatureRigBuilder(날개 옵션)가 프리팹에 붙임. 수치는 생명체별로 이 컴포넌트에서 조정.
/// </summary>
public class WingFlap : MonoBehaviour
{
    [SerializeField] CreatureVisual visual;
    [SerializeField] Transform wingL, wingR;

    [Header("대기 (x~y 사이 무작위)")]
    [Tooltip("한 번 퍼덕이는 시간(초)")] [SerializeField] Vector2 idlePeriod = new Vector2(0.9f, 1.4f);
    [Tooltip("들어 올리는 각도(도)")] [SerializeField] Vector2 idleAmp = new Vector2(5f, 12f);
    [Tooltip("퍼덕임 사이 쉬는 시간(초)")] [SerializeField] Vector2 idlePause = new Vector2(1.2f, 4f);

    [Header("이동 (x~y 사이 무작위)")]
    [SerializeField] Vector2 movePeriod = new Vector2(0.26f, 0.38f);
    [SerializeField] Vector2 moveAmp = new Vector2(24f, 38f);
    [SerializeField] Vector2 movePause = new Vector2(0f, 0.18f);

    [Header("모양")]
    [Tooltip("들어 올릴 때 뒤로 젖히는 비율 (들어 올리는 각도 대비)")] [SerializeField] float sweepRatio = 0.35f;
    [Tooltip("내려칠 때 아래로 내려가는 비율 (들어 올리는 각도 대비)")] [SerializeField] float downRatio = 0.45f;

    struct Wing
    {
        public Transform t;
        public Quaternion rest;
        public float sign;          // 왼쪽 +1, 오른쪽 -1 (좌우 대칭)
        public float timer;
        public bool resting;
        public float uPeriod, uAmp, uPause; // 이번 박자의 무작위 값 (0~1), 현재 이동 정도로 범위 보간
    }

    Wing l, r;

    public void Setup(CreatureVisual v, Transform left, Transform right) { visual = v; wingL = left; wingR = right; }

    void Awake()
    {
        l = Init(wingL, 1f);
        r = Init(wingR, -1f);
    }

    static Wing Init(Transform t, float sign)
    {
        var w = new Wing { t = t, rest = t ? t.localRotation : Quaternion.identity, sign = sign, resting = true };
        Roll(ref w);
        w.timer = Random.value * 1.5f; // 처음부터 어긋나게
        return w;
    }

    static void Roll(ref Wing w) { w.uPeriod = Random.value; w.uAmp = Random.value; w.uPause = Random.value; }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        float move = visual ? visual.MoveBlend : 0f;
        Step(ref l, dt, move);
        Step(ref r, dt, move);
    }

    void Step(ref Wing w, float dt, float move)
    {
        if (!w.t) return;
        w.timer += dt;
        float lift = 0f;
        if (w.resting)
        {
            float pause = Mathf.Lerp(Range(idlePause, w.uPause), Range(movePause, w.uPause), move);
            if (w.timer >= pause) { w.resting = false; w.timer = 0f; }
        }
        if (!w.resting)
        {
            float period = Mathf.Lerp(Range(idlePeriod, w.uPeriod), Range(movePeriod, w.uPeriod), move);
            float ph = w.timer / period;
            if (ph >= 1f) { w.resting = true; w.timer = 0f; Roll(ref w); }
            else
            {
                float amp = Mathf.Lerp(Range(idleAmp, w.uAmp), Range(moveAmp, w.uAmp), move);
                float s = Mathf.Sin(ph * Mathf.PI * 2f); // 위로 → 아래로
                lift = amp * (s > 0f ? s : s * downRatio);
            }
        }
        // 들어 올림 = 앞뒤 축(Z) 회전, 젖힘 = 세로 축(Y) 회전. 좌우는 부호 반전
        w.t.localRotation = w.rest * Quaternion.Euler(0f, w.sign * Mathf.Abs(lift) * sweepRatio, w.sign * lift);
    }

    static float Range(Vector2 r, float u) => r.x + (r.y - r.x) * u;
}
