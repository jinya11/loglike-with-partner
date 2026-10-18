using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 감시자 폭격 (R). 사용하면 맵 전체 탑뷰로 전환되고 시간이 느려짐 → 마우스로 위치 지정 →
/// 좌클릭/R/Enter 확정, 우클릭/Esc 취소(취소 시 쿨타임 10초). 확정 시 현재 감시자가 자신만의 폭격 실행. 쿨타임은 감시자별(기본 45초).
/// StageDirector가 전투 스테이지에 자동 추가.
/// </summary>
public class BombardSkill : Singleton<BombardSkill>
{
    [SerializeField] Key key = Key.R;
    [Tooltip("조준 중 게임 속도")]
    [SerializeField] float aimTimeScale = 0.5f;
    [Tooltip("조준만 하고 취소(Esc/우클릭)했을 때 쿨타임")]
    [SerializeField] float cancelCooldown = 10f;
    [SerializeField] Color markerColor = new Color(1f, 0.5f, 0.3f);

    static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    Transform marker;
    float readyAt;
    int aimFrame = -1;

    public bool Aiming { get; private set; }
    public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);

    void Start()
    {
        // 조준 표시: 폭격 범위 크기의 납작한 원
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "BombardMarker";
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        var fxr = GameManager.I.Config.fxPrefab ? GameManager.I.Config.fxPrefab.GetComponent<Renderer>() : null;
        if (fxr) r.sharedMaterial = fxr.sharedMaterial;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor(ColorId, markerColor);
        r.SetPropertyBlock(mpb);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        marker = go.transform;
        marker.SetParent(transform, false);
        go.SetActive(false);
    }

    void Update()
    {
        var gm = GameManager.I;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        var comp = CompanionBase.Current;

        if (!Aiming)
        {
            if (gm.State == GameState.Playing && !gm.InputBlocked && comp != null && kb != null
                && kb[key].wasPressedThisFrame && Time.time >= readyAt)
                BeginAim(comp);
            return;
        }

        if (gm.State != GameState.Playing) { EndAim(); return; } // 레벨업/패배 등으로 중단
        Vector3 p = AimPoint(mouse);
        marker.position = p + Vector3.up * 0.06f;
        if (Time.frameCount == aimFrame) return;

        bool confirm = (mouse != null && mouse.leftButton.wasPressedThisFrame)
            || (kb != null && (kb[key].wasPressedThisFrame || kb.enterKey.wasPressedThisFrame));
        bool cancel = (mouse != null && mouse.rightButton.wasPressedThisFrame) || (kb != null && kb.escapeKey.wasPressedThisFrame);
        if (cancel) { readyAt = Time.time + cancelCooldown; EndAim(); return; }
        if (!confirm) return;

        var def = gm.Companion;
        readyAt = Time.time + (def != null ? def.bombardCooldown : 45f) * gm.Stats.cooldownMul;
        EndAim();
        comp.BeginBombard(p);
    }

    void BeginAim(CompanionBase comp)
    {
        Aiming = true;
        aimFrame = Time.frameCount;
        GameManager.I.InputLocked = true;
        if (CameraFollow.I) CameraFollow.I.SetOverview(true);
        Time.timeScale = aimTimeScale;
        float d = comp.BombardRadius * GameManager.I.Stats.areaMul * 2f;
        marker.localScale = new Vector3(d, 0.02f, d);
        marker.gameObject.SetActive(true);
    }

    void EndAim()
    {
        Aiming = false;
        marker.gameObject.SetActive(false);
        GameManager.I.InputLocked = false;
        if (CameraFollow.I) CameraFollow.I.SetOverview(false);
        if (GameManager.I.State == GameState.Playing) Time.timeScale = 1f;
    }

    /// <summary>마우스 위치를 바닥(y=0)에 투영. 마우스가 없으면 조준점(플레이어) 위치.</summary>
    Vector3 AimPoint(Mouse mouse)
    {
        var cf = CameraFollow.I;
        Vector3 fallback = PlayerController.I ? PlayerController.I.Position : Vector3.zero;
        if (mouse == null || cf == null || cf.Cam == null) return fallback;
        var ray = cf.Cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float dist)) return fallback;
        Vector3 p = Arena.Clamp(ray.GetPoint(dist));
        p.y = 0f;
        return p;
    }
}
