using UnityEditor;
using UnityEngine;

/// <summary>
/// 생명체 공격 증강 데이터 테이블 기본값 (공통 + 토끼/마녀/새 소녀 전용).
/// 메뉴: Roguelike/Setup Attack Augment Table — 테이블이 비어 있을 때만 채움 (직접 편집한 행은 유지).
/// </summary>
public static partial class PrototypeBuilder
{
    const string AugmentTablePath = Root + "/Resources/Config/AttackAugmentTable.asset";

    [MenuItem("Roguelike/Setup Attack Augment Table")]
    public static void SetupAttackAugmentTable()
    {
        var table = Asset<AttackAugmentTable>(AugmentTablePath);
        if (table.augments == null || table.augments.Length == 0) table.augments = DefaultAugments();
        EditorUtility.SetDirty(table);
        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        cfg.ward.attackAugments = table;
        EditorUtility.SetDirty(cfg.ward);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PrototypeBuilder] 공격 증강 테이블 {table.augments.Length}개 ({AugmentTablePath})");
    }

    static AttackAugmentDef A(string id, string name, string desc, AugmentEffect fx, float v, int max, float w, params string[] creatures)
        => new AttackAugmentDef { id = id, name = name, desc = desc, effect = fx, value = v, maxStacks = max, weight = w, creatures = creatures };

    static AttackAugmentDef[] DefaultAugments()
    {
        const string Rabbit = "토끼", Witch = "마녀", BirdGirl = "새 소녀";
        return new[]
        {
            // ---- 공통 ----
            A("common_count", "다발", "투사체 +1", AugmentEffect.ProjectileCount, 1f, 4, 1f),
            A("common_damage", "날카로움", "공격 피해 +15%", AugmentEffect.Damage, 0.15f, 5, 1f),
            A("common_speed", "연속 공격", "공격 간격 -10%", AugmentEffect.AttackSpeed, 0.1f, 5, 1f),
            A("common_range", "멀리 보기", "사거리 +2", AugmentEffect.Range, 2f, 3, 0.8f),
            A("common_velocity", "빠른 손", "탄속 +15%", AugmentEffect.ProjectileSpeed, 0.15f, 3, 0.6f),

            // ---- 토끼: 당근 던지기 ----
            A("rabbit_split", "당근 쪼개기", "당근이 떨어지면 작은 당근 2개로 갈라짐", AugmentEffect.Split, 2f, 3, 1f, Rabbit),
            A("rabbit_big", "왕당근", "당근 착지 범위 +30%", AugmentEffect.Area, 0.3f, 3, 1f, Rabbit),
            A("rabbit_patch", "당근밭", "착지 지점에 2초간 감속 장판", AugmentEffect.Zone, 2f, 2, 0.8f, Rabbit),
            A("rabbit_bounce", "통통 당근", "당근이 한 번 더 튀어 옆 적에게 떨어짐", AugmentEffect.Chain, 1f, 2, 0.7f, Rabbit),

            // ---- 마녀(엘리): 지팡이 마법탄 ----
            A("witch_chain", "연쇄 마법", "마법탄이 맞은 적에서 가까운 적으로 1번 더 튕김", AugmentEffect.Chain, 1f, 4, 1.2f, Witch),
            A("witch_pierce", "꿰뚫는 별", "마법탄이 적 1마리를 관통", AugmentEffect.Pierce, 1f, 3, 1f, Witch),
            A("witch_homing", "길잡이 마법", "마법탄이 적을 따라감", AugmentEffect.Homing, 1f, 1, 0.8f, Witch),
            A("witch_echo", "메아리 주문", "20% 확률로 한 번 더 시전", AugmentEffect.Echo, 0.2f, 3, 0.8f, Witch),
            A("witch_burn", "불씨 마법", "맞은 적에게 화상 (초당 3)", AugmentEffect.Burn, 3f, 3, 0.8f, Witch),
            A("witch_circle", "마법진", "명중 지점에 1.5초간 피해 장판", AugmentEffect.Zone, 1.5f, 2, 0.6f, Witch),

            // ---- 새 소녀(유나): 머리 위 새가 공격 ----
            A("bird_dive", "급강하", "새가 돌진하며 적 2마리를 관통", AugmentEffect.Pierce, 2f, 3, 1f, BirdGirl),
            A("bird_feathers", "깃털 흩뿌리기", "새가 깃털을 부채꼴로 2개 더 날림", AugmentEffect.Split, 2f, 3, 1f, BirdGirl),
            A("bird_extra", "부지런한 새", "새가 한 번 더 공격", AugmentEffect.Summon, 1f, 2, 0.8f, BirdGirl),
            A("bird_mark", "콕 찍기", "새에게 맞은 적이 받는 피해 +20%", AugmentEffect.Mark, 0.2f, 2, 0.8f, BirdGirl),
            A("bird_gust", "날갯바람", "공격에 넉백 추가", AugmentEffect.Knockback, 6f, 2, 0.6f, BirdGirl),
        };
    }
}
