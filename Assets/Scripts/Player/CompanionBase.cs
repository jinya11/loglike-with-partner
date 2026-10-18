using System.Collections;
using UnityEngine;

/// <summary>
/// 동반자 공통 베이스 (템플릿 메서드 패턴): 아레나 벽 너머 배치, 플레이어 주시, 주기적으로 Act 실행.
/// 동반자별 능력은 Act를, 추후 필살기(컷씬)는 Ultimate를 구현.
/// </summary>
public abstract class CompanionBase : MonoBehaviour
{
    [SerializeField] protected Transform head;
    [SerializeField] protected Renderer[] eyes;
    [SerializeField] float distanceFromEdge = 14f, height = -43f;
    [Tooltip("폭격 범위 반경")]
    [SerializeField] protected float bombardRadius = 8f;
    [SerializeField] protected Color eyeIdle = new Color(0.6f, 0.5f, 1f), eyeActive = new Color(1f, 0.9f, 0.4f);

    static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    MaterialPropertyBlock mpb;
    float timer;

    /// <summary>현재 스테이지의 감시자 (폭격 스킬이 사용).</summary>
    public static CompanionBase Current { get; private set; }
    public float BombardRadius => bombardRadius;

    void OnDestroy() { if (Current == this) Current = null; }

    protected virtual void Start()
    {
        Current = this;
        transform.position = new Vector3(0f, height, StageDirector.I.ArenaRadius + distanceFromEdge);
        mpb = new MaterialPropertyBlock();
        SetEyes(eyeIdle);
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        Vector3 to = PlayerController.I.Position - head.position;
        head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(to), 4f * Time.deltaTime);

        var s = GameManager.I.Stats;
        if ((timer += Time.deltaTime) < s.companionInterval || Enemy.Active.Count == 0) return;
        timer = 0f;
        StartCoroutine(Run(s));
    }

    IEnumerator Run(PlayerStats s)
    {
        SetEyes(eyeActive);
        yield return Act(s);
        SetEyes(eyeIdle);
    }

    /// <summary>주기적으로 실행되는 동반자 고유 능력.</summary>
    protected abstract IEnumerator Act(PlayerStats s);

    /// <summary>플레이어가 지정한 위치에 폭격 (감시자마다 다른 연출/효과).</summary>
    public void BeginBombard(Vector3 target) => StartCoroutine(RunBombard(target));

    IEnumerator RunBombard(Vector3 target)
    {
        SetEyes(eyeActive);
        head.rotation = Quaternion.LookRotation(target - head.position);
        yield return Bombard(target, GameManager.I.Stats);
        SetEyes(eyeIdle);
    }

    protected abstract IEnumerator Bombard(Vector3 target, PlayerStats s);

    /// <summary>추후: 고유 필살기 (컷씬). CompanionDef.ultimateCutscene과 연결 예정.</summary>
    public virtual void Ultimate() { }

    protected void SetEyes(Color c)
    {
        if (mpb == null || eyes == null) return;
        mpb.SetColor(ColorId, c);
        foreach (var e in eyes) e.SetPropertyBlock(mpb);
    }
}
