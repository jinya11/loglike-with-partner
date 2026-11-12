using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 감시자 폭격 (R). 기본(CompanionConfig.bombardAtWatcher): 감시자(조준점)가 폭격 마커 — R을 누르면 감시자가 서 있는 곳에 바로 폭격.
/// 끄면 기존 방식: 사용하면 맵 전체 탑뷰로 전환되고 시간이 느려짐 → 마우스로 위치 지정 →
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
    float autoCheckAt;

    public bool Aiming { get; private set; }
    public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);

    void Start()
    {
        // 조준 표시: 폭격 범위 크기의 납작한 원
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "BombardMarker";
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        var fxr = GameManager.I.Config.stage.fxPrefab ? GameManager.I.Config.stage.fxPrefab.GetComponent<Renderer>() : null;
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
        if (Disabled) { if (Aiming) EndAim(); return; }

        if (!Aiming)
        {
            bool atWatcher = gm.Config.companion.bombardAtWatcher;
            if (gm.State == GameState.Playing && !gm.InputBlocked && comp != null && kb != null && !(Ward.I && Ward.I.WatcherAuto) // 감시자 자동일 때는 직접 조준 불가
                && kb[key].wasPressedThisFrame && Time.time >= readyAt)
            {
                if (atWatcher) FireAtWatcher(comp);
                else BeginAim(comp);
            }
            if (atWatcher) UpdateRangePreview(gm, comp);
            return;
        }

        if (gm.State != GameState.Playing) { EndAim(); return; } // 레벨업/패배 등으로 중단
        Vector3 p = AimPoint(mouse);
        marker.position = p + Vector3.up * 0.06f;
        if (Time.frameCount == aimFrame) return;

        bool confirm = (mouse != null && mouse.leftButton.wasPressedThisFrame && !WardDebugPanel.PointerOver(mouse.position.ReadValue()))
            || (kb != null && (kb[key].wasPressedThisFrame || kb.enterKey.wasPressedThisFrame));
        bool cancel = (mouse != null && mouse.rightButton.wasPressedThisFrame) || (kb != null && kb.escapeKey.wasPressedThisFrame);
        if (cancel) { readyAt = Time.time + cancelCooldown; EndAim(); return; }
        if (!confirm) return;

        var def = gm.Companion;
        readyAt = Time.time + (def != null ? def.bombardCooldown : 45f) * gm.Stats.cooldownMul;
        EndAim();
        comp.BeginBombard(p);
    }

    /// <summary>감시자 = 폭격 마커: 감시자(조준점)가 서 있는 곳에 바로 폭격.</summary>
    void FireAtWatcher(CompanionBase comp)
    {
        var gm = GameManager.I;
        var pc = PlayerController.I;
        if (pc == null) return;
        Vector3 p = Ground.Snap(Arena.Clamp(pc.Position));
        var def = gm.Companion;
        readyAt = Time.time + (def != null ? def.bombardCooldown : 45f) * gm.Stats.cooldownMul;
        Vfx.Pillar(p, markerColor);
        comp.BeginBombard(p);
    }

    /// <summary>폭격이 준비되면 감시자 발밑에 폭격 범위를 옅게 표시 (어디에 떨어질지).</summary>
    void UpdateRangePreview(GameManager gm, CompanionBase comp)
    {
        var pc = PlayerController.I;
        bool show = gm.Config.companion.showBombardRange && comp != null && pc != null && gm.State == GameState.Playing && Time.time >= readyAt;
        if (!show) { if (rangeRing) rangeRing.enabled = false; return; }
        if (!rangeRing)
        {
            // 외곽선 고리 (지형 높이를 따라 그림, 바닥을 가리지 않음)
            rangeRing = new GameObject("BombardRange").AddComponent<LineRenderer>();
            rangeRing.transform.SetParent(transform, false);
            rangeRing.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            rangeRing.useWorldSpace = true; rangeRing.loop = true;
            rangeRing.widthMultiplier = 0.14f;
            rangeRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rangeRing.receiveShadows = false;
            rangeRing.positionCount = RingSeg;
        }
        rangeRing.enabled = true;
        float r = comp.BombardRadius * gm.Stats.areaMul;
        Vector3 c = pc.Position;
        for (int i = 0; i < RingSeg; i++)
        {
            float a = i * (Mathf.PI * 2f / RingSeg);
            float x = c.x + Mathf.Cos(a) * r, z = c.z + Mathf.Sin(a) * r;
            ringBuf[i] = new Vector3(x, Ground.HeightAt(x, z) + 0.1f, z);
        }
        rangeRing.SetPositions(ringBuf);
        var col = markerColor; col.a = 0.55f + 0.25f * Mathf.Sin(Time.time * 4f);
        rangeRing.startColor = rangeRing.endColor = col;
    }

    const int RingSeg = 64;
    static readonly Vector3[] ringBuf = new Vector3[RingSeg];
    LineRenderer rangeRing;

    MaterialPropertyBlock markerMpb;
    void SetMarkerAlpha(float a)
    {
        var r = marker.GetComponent<Renderer>();
        markerMpb ??= new MaterialPropertyBlock();
        var c = markerColor; c.a = a;
        markerMpb.SetColor(ColorId, c);
        r.SetPropertyBlock(markerMpb);
    }

    /// <summary>
    /// 감시자 자동(자율 + 생명체만 조작)일 때 자동 폭격: 쿨타임이 끝나면 생명체 주변(사거리 안)에서 적이 가장 많이 모인 곳에.
    /// 감시자 마커 방식이면 감시자 위치의 폭격 범위 안에 적이 충분히 모였을 때 감시자 위치에.
    /// </summary>
    /// <summary>파생 규칙이 폭격을 막음 (발광 균사 동굴). 켜져 있으면 조준·자동 폭격 모두 안 됨.</summary>
    public static bool Disabled;

    public void TryAuto(Vector3 around)
    {
        if (Disabled) return;
        var comp = CompanionBase.Current;
        var gm = GameManager.I;
        if (Aiming || comp == null || Time.time < readyAt || gm.State != GameState.Playing || Time.time < autoCheckAt) return;
        autoCheckAt = Time.time + 0.5f; // 검사는 0.5초마다
        if (gm.Config.companion.bombardAtWatcher && PlayerController.I)
        {
            Vector3 w = PlayerController.I.Position;
            float rr = comp.BombardRadius * gm.Stats.areaMul;
            int cnt = 0;
            foreach (var e in Enemy.Active) { Vector3 d = e.transform.position - w; d.y = 0f; if (d.sqrMagnitude < rr * rr) cnt++; }
            if (cnt >= gm.Config.companion.autoBombardMinEnemies) FireAtWatcher(comp);
            return;
        }
        float r = comp.BombardRadius * gm.Stats.areaMul, reach = gm.Stats.attackRange + r;
        var list = Enemy.Active;
        int best = 0; Vector3 target = around;
        for (int i = 0; i < list.Count; i += 2) // 후보는 적 위치 (절반만 검사)
        {
            Vector3 c = list[i].transform.position; c.y = 0f;
            if ((c - around).sqrMagnitude > reach * reach) continue;
            int n = 0;
            for (int k = 0; k < list.Count; k++) if ((list[k].transform.position - c).sqrMagnitude < r * r) n++;
            if (n > best) { best = n; target = c; }
        }
        if (best < 5) return; // 적이 충분히 모였을 때만
        var def = gm.Companion;
        readyAt = Time.time + (def != null ? def.bombardCooldown : 45f) * gm.Stats.cooldownMul;
        comp.BeginBombard(Arena.Clamp(target));
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
        SetMarkerAlpha(markerColor.a);
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
        Vector3 p = Ground.Snap(Arena.Clamp(ray.GetPoint(dist)));
        return p;
    }
}
