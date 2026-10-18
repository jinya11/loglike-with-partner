using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 레벨업 순환 업데이트: 증강 분류(탄환/생명체 생존력/동반자) 지정, 부족한 증강 추가,
/// HUD_Stage에 생명체 증강 제목 추가, 경험치 바를 화면 하단 전체 폭으로 이동. (기존 편집 유지)
/// 메뉴: Roguelike/Apply Upgrade Cycle Update
/// </summary>
public static partial class PrototypeBuilder
{
    [MenuItem("Roguelike/Apply Upgrade Cycle Update")]
    public static void ApplyUpgradeCycleUpdate()
    {
        EnsureFonts();
        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        AddUpgrade(cfg, new UpgradeDef { name = "장거리 탄도", desc = "공격 사거리 +3", stat = StatType.AttackRange, value = 3f, weight = 1f });
        AddUpgrade(cfg, new UpgradeDef { name = "가속 탄환", desc = "탄속 +5", stat = StatType.ProjectileSpeed, value = 5f, weight = 1f });
        AddUpgrade(cfg, new UpgradeDef { name = "새싹의 숨결", desc = "생명체 초당 회복 +0.6", stat = StatType.WardRegen, value = 0.6f, weight = 1f });
        AddUpgrade(cfg, new UpgradeDef { name = "재빠른 발", desc = "생명체 이동 속도 +0.5", stat = StatType.WardSpeed, value = 0.5f, weight = 1f });
        foreach (var u in cfg.upgrades) u.category = CategoryOf(u.stat);
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();

        string path = Root + "/Prefabs/UI/HUD_Stage.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var so = new SerializedObject(root.GetComponent<Hud>());
            if (so.FindProperty("wardTitle").objectReferenceValue == null)
            {
                var normal = so.FindProperty("normalTitle").objectReferenceValue as GameObject;
                var ward = Object.Instantiate(normal, normal.transform.parent);
                ward.name = "WardTitle";
                if (ward.TryGetComponent(out TMP_Text wt)) { wt.text = "생명체 생존력 강화"; wt.color = new Color(0.55f, 1f, 0.55f); }
                ward.SetActive(false);
                so.FindProperty("wardTitle").objectReferenceValue = ward;
            }
            // 경험치 바: 화면 하단, 왼쪽 끝에서 오른쪽 끝까지
            var xp = (so.FindProperty("xpFill").objectReferenceValue as RectTransform).parent as RectTransform;
            xp.anchorMin = new Vector2(0f, 0f);
            xp.anchorMax = new Vector2(1f, 0f);
            xp.pivot = new Vector2(0.5f, 0f);
            xp.anchoredPosition = Vector2.zero;
            xp.sizeDelta = new Vector2(0f, 18f);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("[PrototypeBuilder] 레벨업 순환 업데이트 완료");
    }

    static UpgradeCategory CategoryOf(StatType s)
    {
        switch (s)
        {
            case StatType.WardMaxHp: case StatType.WardHeal: case StatType.WardPulseDamage:
            case StatType.WardPulseRate: case StatType.WardRegen: case StatType.WardSpeed:
                return UpgradeCategory.Ward;
            case StatType.CompanionDamage: case StatType.CompanionRate: case StatType.CompanionStrikes: case StatType.Area:
                return UpgradeCategory.Companion;
            default:
                return UpgradeCategory.Projectile; // 피해, 연사, 다중 사격, 사거리, 탄속, 획득 범위, 조준점 속도, 쿨타임
        }
    }
}
