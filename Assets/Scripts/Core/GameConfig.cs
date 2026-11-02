using UnityEngine;

/// <summary>
/// 설정 모음 (Resources/GameConfig). 실제 수치는 섹션별 설정 파일(Assets/Prototype/Config)에 있음.
/// 코드에서는 GameManager.I.Config.stage.spawnInterval 처럼 섹션을 거쳐 접근.
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/Game Config")]
public class GameConfig : ScriptableObject
{
    [Tooltip("스테이지 진행 / 스폰 / 난이도 디버프")] public StageConfig stage;
    [Tooltip("적 능력치 / 종류")] public EnemyConfig enemy;
    [Tooltip("감시자(동반자)")] public CompanionConfig companion;
    [Tooltip("지켜야 할 작은 생명체 (능력치 + AI)")] public WardConfig ward;
    [Tooltip("플레이어 능력치 / 레벨업 / 강화")] public PlayerConfig player;
    [Tooltip("자원 / 거점 성장")] public MetaConfig meta;

    /// <summary>런 시작 능력치: 플레이어 기본값 + 생명체/감시자 기본값.</summary>
    public PlayerStats CreateStats()
    {
        var s = player.baseStats.Clone();
        ward.ApplyTo(s);
        companion.ApplyTo(s);
        s.wardHp = s.wardMaxHp;
        return s;
    }

#if UNITY_EDITOR
    const string SectionDir = "Assets/Prototype/Resources/Config";

    /// <summary>에디터 빌더용: 빠진 섹션 설정 파일을 만들어 연결.</summary>
    public void EnsureSections()
    {
        stage = Section(stage, "StageConfig"); enemy = Section(enemy, "EnemyConfig");
        companion = Section(companion, "CompanionConfig"); ward = Section(ward, "WardConfig");
        player = Section(player, "PlayerConfig"); meta = Section(meta, "MetaConfig");
    }

    static T Section<T>(T cur, string name) where T : ScriptableObject
    {
        if (cur) return cur;
        string path = $"{SectionDir}/{name}.asset";
        var a = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        if (a) return a;
        if (!UnityEditor.AssetDatabase.IsValidFolder(SectionDir)) UnityEditor.AssetDatabase.CreateFolder("Assets/Prototype/Resources", "Config");
        a = CreateInstance<T>();
        UnityEditor.AssetDatabase.CreateAsset(a, path);
        return a;
    }

    /// <summary>에디터 빌더용: 이 설정과 모든 섹션을 저장 대상으로 표시.</summary>
    public void SetAllDirty()
    {
        foreach (var o in new Object[] { this, stage, enemy, companion, ward, player, meta })
            if (o) UnityEditor.EditorUtility.SetDirty(o);
    }
#endif
}
