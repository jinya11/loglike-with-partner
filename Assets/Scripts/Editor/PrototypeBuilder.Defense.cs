using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 디펜스 컨셉 업데이트: 작은 생명체(Ward) 프리팹, 관련 강화, HUD_Stage에 생명체 체력/시점 표시 추가.
/// 기존 씬과 프리팹의 사용자 편집은 유지 (없는 요소만 추가). 메뉴: Roguelike/Apply Defense Update
/// </summary>
public static partial class PrototypeBuilder
{
    [MenuItem("Roguelike/Apply Defense Update")]
    public static void ApplyDefenseUpdate()
    {
        EnsureFonts();
        int wardLayer = EnsureLayer("Ward");
        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");

        cfg.wardPrefab = BuildWard(wardLayer);
        var b = cfg.baseStats;
        if (b.wardMaxHp <= 0f) { b.wardMaxHp = 60f; b.wardRegen = 0.4f; }
        if (b.wardSpeed <= 0f || Mathf.Approximately(b.wardSpeed, 5.5f)) b.wardSpeed = 3.2f; // 도망 속도 (배회는 절반)
        if (b.wardPulseInterval <= 0f) { b.wardPulseDamage = 3f; b.wardPulseRadius = 2.2f; b.wardPulseInterval = 6f; }
        if (cfg.enemyAggroRadius <= 0f) cfg.enemyAggroRadius = 3.5f;

        AddUpgrade(cfg, new UpgradeDef { name = "보호의 껍질", desc = "생명체 최대 체력 +25", stat = StatType.WardMaxHp, value = 25f, weight = 1f });
        AddUpgrade(cfg, new UpgradeDef { name = "생명의 이슬", desc = "생명체 체력 30 회복", stat = StatType.WardHeal, value = 30f, weight = 1f });
        AddUpgrade(cfg, new UpgradeDef { name = "작은 반항", desc = "생명체 저항 파동 피해 +4, 범위 증가", stat = StatType.WardPulseDamage, value = 4f, special = true, weight = 1f });
        AddUpgrade(cfg, new UpgradeDef { name = "작은 용기", desc = "생명체 저항 파동 주기 -25%", stat = StatType.WardPulseRate, value = 0.25f, special = true, weight = 1f });
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();

        UpdateStageHud();
        Debug.Log("[PrototypeBuilder] 디펜스 업데이트 적용 완료");
    }

    static void ReplaceIfDefault(SerializedObject so, string field, string oldText, string newText)
    {
        var t = so.FindProperty(field).objectReferenceValue as TMP_Text;
        if (t && t.text == oldText) t.text = newText;
    }

    static void AddUpgrade(GameConfig cfg, UpgradeDef u)
    {
        if (System.Array.FindIndex(cfg.upgrades, x => x.stat == u.stat) < 0) ArrayUtility.Add(ref cfg.upgrades, u);
    }

    /// <summary>작은 생명체: 연두색 동그란 몸, 새싹, 큰 눈.</summary>
    static Ward BuildWard(int layer)
    {
        var root = new GameObject("Ward") { layer = layer };
        var cc = root.AddComponent<CharacterController>();
        cc.center = new Vector3(0, 0.35f, 0); cc.height = 0.7f; cc.radius = 0.3f; cc.stepOffset = 0.2f;
        var body = new GameObject("Body") { layer = layer }.transform; body.SetParent(root.transform, false);
        var skin = Prim(PrimitiveType.Sphere, "Skin", body, new Vector3(0, 0.33f, 0), new Vector3(0.62f, 0.58f, 0.62f), Mat("WardSkin", new Color(0.75f, 1f, 0.7f)));
        var eyeMat = Mat("WardEye", new Color(0.08f, 0.08f, 0.1f));
        Prim(PrimitiveType.Sphere, "EyeL", body, new Vector3(-0.12f, 0.4f, 0.26f), new Vector3(0.1f, 0.14f, 0.06f), eyeMat);
        Prim(PrimitiveType.Sphere, "EyeR", body, new Vector3(0.12f, 0.4f, 0.26f), new Vector3(0.1f, 0.14f, 0.06f), eyeMat);
        var leaf = Mat("WardLeaf", new Color(0.35f, 0.75f, 0.3f));
        Prim(PrimitiveType.Cylinder, "Stem", body, new Vector3(0, 0.66f, 0), new Vector3(0.03f, 0.06f, 0.03f), leaf);
        Prim(PrimitiveType.Sphere, "LeafL", body, new Vector3(-0.08f, 0.73f, 0), new Vector3(0.16f, 0.04f, 0.08f), leaf).transform.localRotation = Quaternion.Euler(0, 0, 20);
        Prim(PrimitiveType.Sphere, "LeafR", body, new Vector3(0.08f, 0.73f, 0), new Vector3(0.16f, 0.04f, 0.08f), leaf).transform.localRotation = Quaternion.Euler(0, 0, -20);
        var eye = new GameObject("Eye").transform; eye.SetParent(body, false); eye.localPosition = new Vector3(0, 0.45f, 0.32f);

        var ward = root.AddComponent<Ward>();
        var so = new SerializedObject(ward);
        so.FindProperty("body").objectReferenceValue = body;
        so.FindProperty("eye").objectReferenceValue = eye;
        SetArray(so.FindProperty("tintRenderers"), new Object[] { skin.GetComponent<Renderer>() });
        so.ApplyModifiedPropertiesWithoutUndo();
        return Save(root, "Ward").GetComponent<Ward>();
    }

    /// <summary>HUD_Stage 프리팹에 없는 요소만 추가 (사용자가 편집한 내용은 그대로).</summary>
    static void UpdateStageHud()
    {
        string path = Root + "/Prefabs/UI/HUD_Stage.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var hud = root.GetComponent<Hud>();
            var so = new SerializedObject(hud);
            var t = root.transform;

            if (so.FindProperty("wardFill").objectReferenceValue == null)
            {
                var fill = Bar("WardBar", t, new Vector2(20, -150), new Vector2(420, 24), new Color(0.45f, 0.9f, 0.45f));
                var text = Txt("WardText", fill.parent, "생명체 {0} / {1}", 18, TextAlignmentOptions.Left, Color.white, Vector2.zero, Vector2.one, new Vector2(10, 0), Vector2.zero);
                so.FindProperty("wardFill").objectReferenceValue = fill;
                so.FindProperty("wardText").objectReferenceValue = text;
            }
            if (so.FindProperty("cameraModeText").objectReferenceValue == null)
            {
                var tr = new Vector2(1, 1);
                var cam = Txt("CameraModeText", t, "[V] 시점: {0}", 22, TextAlignmentOptions.Right, Color.white, tr, tr, new Vector2(-20, -20), new Vector2(600, 32), tr);
                so.FindProperty("cameraModeText").objectReferenceValue = cam;
            }
            if (so.FindProperty("bombardText").objectReferenceValue == null)
            {
                var bt = Txt("BombardText", t, "[R] 감시자 폭격 {0}", 22, TextAlignmentOptions.Left, new Color(1f, 0.75f, 0.5f), TL, TL, new Vector2(20, -180), new Vector2(600, 32), TL);
                so.FindProperty("bombardText").objectReferenceValue = bt;
                var hint = Txt("BombardAimHint", t, "폭격 위치 지정: 좌클릭 확정  /  우클릭·Esc 취소", 34, TextAlignmentOptions.Center, new Color(1f, 0.75f, 0.5f), TC, TC, new Vector2(0, -110), new Vector2(1400, 56));
                hint.gameObject.SetActive(false);
                so.FindProperty("bombardAimHint").objectReferenceValue = hint.gameObject;
            }
            if (so.FindProperty("wardLostTitle").objectReferenceValue == null)
            {
                var res = (so.FindProperty("resultPanel").objectReferenceValue as GameObject).transform;
                var lost = Txt("WardLostTitle", res, "생명체를 지키지 못했습니다", 60, TextAlignmentOptions.Center, new Color(1f, 0.55f, 0.5f), C, C, new Vector2(0, 40), new Vector2(1400, 90));
                lost.gameObject.SetActive(false);
                so.FindProperty("wardLostTitle").objectReferenceValue = lost.gameObject;
            }
            // 기본 문구 그대로인 경우에만 새 규칙에 맞게 교체 (직접 바꾼 문구는 유지)
            ReplaceIfDefault(so, "skillText", "[Q] 충격파 {0}    [E] 대시 {1}", "[Q] 충격파 {0}    [E] 보호막 {1}");
            var pm = so.FindProperty("portalMessage").objectReferenceValue as GameObject;
            if (pm && pm.TryGetComponent(out TMP_Text pt) && pt.text == "포털이 열렸습니다. 중앙에서 F로 이동하세요")
                pt.text = "포털이 열렸습니다. 생명체가 포털로 향합니다";
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
