using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>감시자 스킬 종류.</summary>
public enum WatcherSkillType { Decoy, Control, TripleBombard, Bombard, Thorns, Parry }

/// <summary>감시자 스킬 분류 (UI 표시용).</summary>
public enum WatcherSkillCategory { 유도, 폭격, 방어 }

/// <summary>스킬 한 줄 (공통 항목). 스킬별 고유 수치는 WatcherSkillConfig의 각 섹션.</summary>
[System.Serializable]
public class WatcherSkillDef
{
    public WatcherSkillType type;
    public string name;
    public WatcherSkillCategory category;
    [Tooltip("로비 UI 설명 한 줄")] public string oneLine;
    [Tooltip("재사용 대기(초)")] public float cooldown = 10f;
    [Tooltip("코스트")] public int cost = 2;
    [Tooltip("기본 해금 (해금 조건 로직은 아직 없음)")] public bool unlockedByDefault = true;
    [Header("성장 (별도 성장 화면에서, 인게임 재화로 — 아직 미구현)")]
    [Tooltip("최대 레벨")] public int maxLevel = 5;
    [Tooltip("레벨이 1 오를 때마다 효과 수치 배율 (+비율). 레벨 0 = 기본")] public float effectPerLevel = 0.1f;
    [Tooltip("레벨이 1 오를 때마다 재사용 대기 감소 (비율)")] public float cooldownPerLevel = 0f;

    public WatcherSkillDef() { }
    public WatcherSkillDef(WatcherSkillType t, string n, WatcherSkillCategory c, string line, float cd, int cost)
    { type = t; name = n; category = c; oneLine = line; cooldown = cd; this.cost = cost; }
}

[System.Serializable] public class DecoySkillSettings
{
    [Tooltip("설치 후 작동까지(초)")] public float armDelay = 0.5f;
    [Tooltip("적을 끌어당기는 원형 범위(m)")] public float radius = 9f;
    [Tooltip("작동 시간(초)")] public float duration = 6f;
}

[System.Serializable] public class ControlSkillSettings
{
    [Tooltip("시간이 멈추고 적을 고르는 시간(초, 실제 시간)")] public float pickTime = 5f;
    [Tooltip("고를 수 있는 적 수")] public int maxPicks = 3;
    [Tooltip("클릭 지점에서 이 거리 안의 가장 가까운 적을 고름(m)")] public float pickRadius = 2.5f;
    [Tooltip("고른 적이 혼돈 상태가 될 확률")] [Range(0, 1)] public float chaosChance = 0.7f;
    [Tooltip("혼돈 상태 지속(초)")] public float chaosDuration = 8f;
    [Tooltip("혼돈 적이 주는 피해 = 원래 접촉 피해 × 이 값 (많이 낮게)")] public float chaosDamageMul = 0.25f;
    [Tooltip("혼돈 적이 다른 적을 때리는 거리(m)")] public float chaosHitRange = 1.3f;
}

[System.Serializable] public class TripleBombardSettings
{
    [Tooltip("고를 수 있는 폭격 지점 수")] public int points = 3;
    [Tooltip("클릭 후 폭격까지(초)")] public float delay = 0.3f;
    [Tooltip("폭격 범위 반경(m)")] public float radius = 4f;
    [Tooltip("피해 = 감시자 피해 × 이 값")] public float damageMul = 3f;
    [Tooltip("지점을 고르는 동안 게임 속도")] public float aimTimeScale = 0.4f;
}

[System.Serializable] public class ThornsSkillSettings
{
    [Tooltip("반사 횟수")] public int reflects = 5;
    [Tooltip("반사 피해 = 적이 준 피해 × 이 값")] public float reflectFrac = 0.5f;
    [Tooltip("반사 상태 최대 지속(초) — 횟수를 다 쓰지 못해도 끝남")] public float maxDuration = 20f;
    [Tooltip("반사로 죽은 적 자리에 생기는 가시 유지(초)")] public float spikeLife = 10f;
    [Tooltip("가시 반경(m)")] public float spikeRadius = 1.3f;
    [Tooltip("가시를 밟은 적이 받는 초당 피해")] public float spikeDps = 8f;
}

[System.Serializable] public class ParrySkillSettings
{
    [Tooltip("판정 시간 창(초): 키를 누른 뒤 이 안에 적이 공격하면 성공")] public float window = 0.25f;
    [Tooltip("성공 시 생명체 무적(초)")] public float invulnerable = 1.3f;
    [Tooltip("성공 시 주변 적 넉백 반경(m)")] public float knockRadius = 6f;
    [Tooltip("넉백 속도")] public float knockSpeed = 12f;
}

/// <summary>
/// 감시자 스킬 설정 (Assets/Prototype/Resources/Config/WatcherSkillConfig.asset, GameConfig.skills).
/// 코스트·재사용 대기·범위·시간·피해 등 모든 수치와 키를 여기서 조절. 코드에 수치 없음.
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Watcher Skill")]
public class WatcherSkillConfig : ScriptableObject
{
    [Header("키")]
    [Tooltip("장착 칸 1, 2 사용 키")] public Key[] slotKeys = { Key.Digit1, Key.Digit2 };
    [Tooltip("스킬 장착 창 열기 (로비)")] public Key menuKey = Key.U;
    [Tooltip("장착 칸 수")] public int slotCount = 2;

    [Header("코스트")]
    [Tooltip("최대 코스트")] public int maxCost = 5;
    [Tooltip("첫 스테이지 입장 시 코스트")] public float startCost = 3f;
    [Tooltip("적 처치 1회당 충전량 (1 = 코스트 1)")] public float costPerKill = 0.2f;
    [Tooltip("환경사(처치 수에 안 치는 죽음)도 충전할지")] public bool chargeFromEnvironmentKills = false;

    [Header("스킬 목록")]
    public WatcherSkillDef[] skills =
    {
        new WatcherSkillDef(WatcherSkillType.Decoy, "디코이", WatcherSkillCategory.유도, "범위 안의 적을 설치한 장치로 끌어들인다", 5f, 2),
        new WatcherSkillDef(WatcherSkillType.Control, "조종", WatcherSkillCategory.유도, "시간을 멈추고 고른 적을 혼돈에 빠뜨린다", 15f, 5),
        new WatcherSkillDef(WatcherSkillType.TripleBombard, "3중 폭격", WatcherSkillCategory.폭격, "탑뷰에서 세 곳을 골라 폭격한다", 10f, 4),
        new WatcherSkillDef(WatcherSkillType.Bombard, "폭격", WatcherSkillCategory.폭격, "감시자 위치에 폭격한다", 20f, 5),
        new WatcherSkillDef(WatcherSkillType.Thorns, "가시", WatcherSkillCategory.방어, "생명체를 때린 적에게 피해를 되돌려준다", 10f, 3),
        new WatcherSkillDef(WatcherSkillType.Parry, "패링", WatcherSkillCategory.방어, "공격 순간에 맞춰 쓰면 적을 쓰러뜨린다", 3f, 1),
    };

    [Header("스킬별 수치")]
    public DecoySkillSettings decoy = new DecoySkillSettings();
    public ControlSkillSettings control = new ControlSkillSettings();
    public TripleBombardSettings tripleBombard = new TripleBombardSettings();
    public ThornsSkillSettings thorns = new ThornsSkillSettings();
    public ParrySkillSettings parry = new ParrySkillSettings();

    public WatcherSkillDef Get(WatcherSkillType t)
    {
        foreach (var s in skills) if (s != null && s.type == t) return s;
        return null;
    }

    /// <summary>성장 레벨을 반영한 효과 배율 (레벨 0 = 1).</summary>
    public float EffectMul(WatcherSkillType t) { var d = Get(t); return d == null ? 1f : 1f + d.effectPerLevel * SaveData.SkillLevel(t); }

    /// <summary>성장 레벨을 반영한 재사용 대기.</summary>
    public float Cooldown(WatcherSkillType t) { var d = Get(t); return d == null ? 0f : d.cooldown * Mathf.Max(0.1f, 1f - d.cooldownPerLevel * SaveData.SkillLevel(t)); }
}
