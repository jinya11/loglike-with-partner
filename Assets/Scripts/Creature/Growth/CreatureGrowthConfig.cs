using UnityEngine;

/// <summary>성장 노드 종류. Main = 평타 방식 변경(서로 배타 선택), Stat = 기본 스탯, Passive = 특수 효과, Watcher = 감시자 스킬 연계.</summary>
public enum GrowthNodeKind { Main, Stat, Passive, Watcher }

/// <summary>Main/Passive/Watcher 노드가 켜는 효과 (Stat 노드는 None).</summary>
public enum GrowthEffect
{
    None,
    PierceCarrot,   // 메인: 관통 당근
    SmallCarrots,   // 메인: 소형 당근 (부채꼴)
    BounceCarrot,   // 패시브: 팅기는 당근
    CarrotSkewer,   // 패시브: 당근 꼬치
    Lifesteal,      // 패시브: 피흡 (생명체 회복)
    CarrotRain,     // 감시자: 폭격 스킬 발동 시 당근 폭격
    // ---- 마법사 ----
    ArcaneChain,    // 메인: 연쇄 (번개가 주변 적에게 연쇄)
    ArcaneBurn,     // 메인: 연소 (지속 피해)
    ArcaneFreeze,   // 메인: 빙결 (확률로 얼림)
    ArcaneCurse,    // 메인: 저주 (확률로 받는 피해 증가)
    ArcaneAll,      // 패시브: 메인 효과 전부 + 연쇄마다 모든 효과
    ArcaneOrbs,     // 감시자: 유도(디코이) 스킬 발동 시 거대 마법 투사체
    // ---- 새 소녀 ----
    WingLift,       // 메인: 날개짓 (맞은 적을 띄움)
    WingDash,       // 메인: 돌진 (확률로 피해 무시 + 전방 돌진)
    ScatterWings,   // 메인: 거침없는 날개 (깃털 확정 드롭, 주우면 4방향 깃털)
    Momentum,       // 패시브: 맞지 않으면 공속 스택
    FeatherDrop,    // 패시브: 명중 시 확률로 깃털 드롭 (주우면 이속/공속 증가)
    SpeedGuard,     // 패시브: 빠를수록 받는 피해 감소
    BirdGust,       // 감시자: 폭격 시 새 소녀 앞쪽 가로 돌풍 (피해 없음, 넉백)
}

/// <summary>성장 노드 하나. 인스펙터에서 행을 추가하면 노드가 늘어남 (Stat 노드는 자유롭게 추가 가능).</summary>
[System.Serializable]
public class GrowthNodeDef
{
    [Tooltip("고유 id (저장 키). 바꾸면 해금 기록이 끊김")] public string id;
    public string name;
    [TextArea(1, 4)] public string desc;
    public GrowthNodeKind kind;
    [Tooltip("노드 화면 위치 (0~1 정규화, 왼쪽 아래 = 0,0)")] public Vector2 position;
    [Tooltip("선행 노드 id — 이 중 하나라도 해금되어 있으면 해금 가능. 비우면 시작 노드")] public string[] requires;
    [Tooltip("필요 성장 재화")] public int cost = 1;
    [Tooltip("추가 해금 조건: 이 생명체로 클리어한 횟수 (0 = 조건 없음)")] public int requiredClears;
    [Header("Stat 노드")]
    public StatType stat;
    public float value;
    [Header("Main / Passive / Watcher 노드")]
    public GrowthEffect effect;
}

/// <summary>
/// 생명체 하나의 성장 노드(스킬 트리) 설정. CreatureDef.growth에 연결 (GameConfig → WardConfig → creatures).
/// 레이아웃(노드 위치)도 데이터: 토끼 = 당근 외곽선, 이후 마법사(마법진)/새 소녀(새 외곽) 같은 구조로 추가.
/// 기본값은 메뉴 Roguelike/Setup Rabbit Growth.
/// </summary>

public abstract class CreatureGrowthConfig : ScriptableObject
{
    [Tooltip("CreatureDef.name과 같게 (저장 키 접두사)")] public string creatureName = "토끼";
    [Tooltip("노드 화면 제목")] public string title = "토끼의 성장";
    [Tooltip("UI에 표시할 이름 (클리어 조건 문구 등)")] public string displayName = "토끼";
    public GrowthNodeDef[] nodes;

    [Header("레이아웃")]
    [Tooltip("노드 보드 크기 (px, 1920x1080 기준)")] public Vector2 boardSize = new Vector2(820, 960);
    [Tooltip("보드 뒤 흐린 실루엣 (비우면 없음)")] public Sprite silhouette;
    [Tooltip("노드 색: 잠김 / 해금 가능 / 해금됨")]
    public Color lockedColor = new Color(0.86f, 0.82f, 0.86f), availableColor = new Color(1f, 0.8f, 0.62f), ownedColor = new Color(1f, 0.6f, 0.55f);
    [Tooltip("종류별 강조 색 (Main/Stat/Passive/Watcher)")]
    public Color mainColor = new Color(1f, 0.55f, 0.6f), statColor = new Color(1f, 0.9f, 0.7f), passiveColor = new Color(0.78f, 0.7f, 1f), watcherColor = new Color(0.62f, 0.8f, 1f);

    [Header("감시자 스킬 추천 (이 생명체를 고른 상태면 해당 분류 스킬에 '추천')")]
    public bool recommendSkills = true;
    public WatcherSkillCategory recommendedCategory = WatcherSkillCategory.폭격;



    public GrowthNodeDef Find(string id)
    {
        if (nodes != null) foreach (var n in nodes) if (n != null && n.id == id) return n;
        return null;
    }

    public Color KindColor(GrowthNodeKind k) => k switch
    {
        GrowthNodeKind.Main => mainColor, GrowthNodeKind.Passive => passiveColor, GrowthNodeKind.Watcher => watcherColor, _ => statColor,
    };
}
