using UnityEngine;

/// <summary>로비 상호작용 오브젝트 (난이도 석판/동반자 제단/출발 포털). F 키로 작동.</summary>
public class LobbyInteractable : Interactable
{
    // 값 고정: 씬에 저장된 정수 값이 바뀌지 않도록 (1번은 제거된 무기 받침대)
    public enum Kind { Modifier = 0, Companion = 2, Start = 3 }

    static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    static MaterialPropertyBlock mpb;

    public Kind kind;
    public int index;
    [Tooltip("상호작용 범위에 들어갔을 때 표시할 문구. 비우면 기본 문구 사용")]
    [SerializeField] string customPrompt;
    [Tooltip("활성/선택 상태를 표시할 렌더러 (석판의 룬 등)")]
    [SerializeField] Renderer indicator;
    [SerializeField] Color offColor = new Color(0.2f, 0.2f, 0.25f), onColor = new Color(1f, 0.35f, 0.25f);

    void Start() => Refresh();

    public bool IsOn
    {
        get
        {
            var gm = GameManager.I;
            switch (kind)
            {
                case Kind.Modifier: return gm.ModifierMask != 0;
                case Kind.Companion: return gm.CompanionIndex == index;
                default: return false;
            }
        }
    }

    public override string Prompt
    {
        get
        {
            if (!string.IsNullOrEmpty(customPrompt)) return customPrompt;
            var c = GameManager.I.Config;
            switch (kind)
            {
                case Kind.Modifier: return "[F] 난이도 조절하기";
                case Kind.Companion:
                    var p = c.companion.companions[index];
                    return Locked ? $"<color=#FFB060>[잠김] {p.name}</color>  동반자 고르기 장소에서 해금 ({CostText(p)})" : $"[F] 동반자 선택: {p.name}  ({p.desc})";
                default: return "[F] 출발할 지역 고르기";
            }
        }
    }

    public override void Interact()
    {
        var gm = GameManager.I;
        switch (kind)
        {
            case Kind.Modifier: LobbyDirector.I.OpenDifficulty(); return;
            case Kind.Companion:
                if (!SaveData.IsUnlocked(gm.Config.companion.companions[index])) return; // 잠긴 감시자는 동반자 선택 장소에서 해금
                gm.CompanionIndex = index; break;
            case Kind.Start: RegionSelectUI.Open(); return;
        }
        RefreshAll();
    }

    /// <summary>잠금/선택 상태가 바뀔 때마다 증가 (안내 문구 갱신용).</summary>
    public static int Version { get; private set; }

    public static void RefreshAll()
    {
        Version++;
        foreach (var i in All) if (i is LobbyInteractable li) li.Refresh();
    }

    /// <summary>잠긴 감시자 제단인지.</summary>
    public bool Locked
    {
        get
        {
            if (kind != Kind.Companion) return false;
            var list = GameManager.I.Config.companion.companions;
            return index < list.Length && !SaveData.IsUnlocked(list[index]);
        }
    }

    static string CostText(CompanionDef def)
    {
        var meta = GameManager.I.Config.meta;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < def.unlockCost.Length; i++)
        {
            var c = def.unlockCost[i];
            var r = meta.Resource(c.type);
            if (i > 0) sb.Append(", ");
            sb.Append(r != null ? r.name : c.type.ToString()).Append(' ').Append(c.amount);
        }
        return sb.ToString();
    }

    public void Refresh()
    {
        bool locked = Locked;
        if (kind == Kind.Companion) SetLockMarker(locked);
        if (!indicator) return;
        mpb ??= new MaterialPropertyBlock();
        mpb.SetColor(ColorId, locked ? lockedColor : IsOn ? onColor : offColor);
        indicator.SetPropertyBlock(mpb);
    }

    [Tooltip("잠긴 감시자 제단 색 / 자물쇠 표시 높이")]
    [SerializeField] Color lockedColor = new Color(0.55f, 0.12f, 0.1f);
    [SerializeField] float lockHeight = 3.4f;
    GameObject lockMarker;

    /// <summary>잠긴 제단 위에 떠 있는 자물쇠 (기본 도형으로 생성, 한 번만).</summary>
    void SetLockMarker(bool on)
    {
        if (!on) { if (lockMarker) lockMarker.SetActive(false); return; }
        if (!lockMarker)
        {
            lockMarker = new GameObject("LockMarker");
            lockMarker.transform.SetParent(transform, false);
            lockMarker.transform.position = transform.position + Vector3.up * lockHeight;
            var lockMpb = new MaterialPropertyBlock();
            lockMpb.SetColor(ColorId, new Color(1f, 0.7f, 0.25f));
            Part(lockMarker.transform, new Vector3(0f, 0f, 0f), new Vector3(0.8f, 0.6f, 0.3f), lockMpb);       // 몸통
            Part(lockMarker.transform, new Vector3(-0.27f, 0.45f, 0f), new Vector3(0.1f, 0.35f, 0.1f), lockMpb); // 고리 왼쪽
            Part(lockMarker.transform, new Vector3(0.27f, 0.45f, 0f), new Vector3(0.1f, 0.35f, 0.1f), lockMpb);  // 고리 오른쪽
            Part(lockMarker.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.64f, 0.1f, 0.1f), lockMpb);     // 고리 위
            lockMarker.AddComponent<Billboard>();
        }
        lockMarker.SetActive(true);
    }

    void Part(Transform parent, Vector3 pos, Vector3 scale, MaterialPropertyBlock m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        if (indicator) r.sharedMaterial = indicator.sharedMaterial;
        r.SetPropertyBlock(m);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
