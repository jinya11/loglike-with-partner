using UnityEngine;

/// <summary>로비 상호작용 오브젝트 (석판/무기 받침대/동반자 제단/출발 포털). F 키로 작동.</summary>
public class LobbyInteractable : Interactable
{
    public enum Kind { Modifier, Weapon, Companion, Start }

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
                case Kind.Weapon: return gm.WeaponIndex == index;
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
                case Kind.Weapon: var w = c.weapons[index]; return $"[F] 무기 장착: {w.name}  ({w.desc})";
                case Kind.Companion: var p = c.companions[index]; return $"[F] 동반자 선택: {p.name}  ({p.desc})";
                default: return "[F] 출발";
            }
        }
    }

    public override void Interact()
    {
        var gm = GameManager.I;
        switch (kind)
        {
            case Kind.Modifier: LobbyDirector.I.OpenDifficulty(); return;
            case Kind.Weapon: gm.WeaponIndex = index; gm.ResetRun(); PlayerController.I.RefreshWeapon(); break;
            case Kind.Companion: gm.CompanionIndex = index; break;
            case Kind.Start: gm.StartRun(); return;
        }
        RefreshAll();
    }

    public static void RefreshAll()
    {
        foreach (var i in All) if (i is LobbyInteractable li) li.Refresh();
    }

    public void Refresh()
    {
        if (!indicator) return;
        mpb ??= new MaterialPropertyBlock();
        mpb.SetColor(ColorId, IsOn ? onColor : offColor);
        indicator.SetPropertyBlock(mpb);
    }
}
