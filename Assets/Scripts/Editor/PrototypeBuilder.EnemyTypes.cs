using UnityEditor;
using UnityEngine;

/// <summary>
/// 근접 디버프형 적 종류 추가 (원거리형 없음). 프리팹 생성 + GameConfig.enemyTypes 등록.
/// 메뉴: Roguelike/Apply Enemy Types Update (이미 등록된 종류는 수치 유지, 프리팹만 갱신)
/// </summary>
public static partial class PrototypeBuilder
{
    [MenuItem("Roguelike/Apply Enemy Types Update")]
    public static void ApplyEnemyTypesUpdate()
    {
        enemyLayer = EnsureLayer("Enemy");
        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        var eyeMat = Mat("EnemyEye", Color.yellow);

        var charger = BuildEnemyVariant("Enemy_Charger", 0.5f, v =>
        {
            var m = Mat("EnemyCharger", new Color(1f, 0.55f, 0.15f));
            Prim(PrimitiveType.Cube, "Body", v, new Vector3(0, 0.5f, 0), new Vector3(0.9f, 0.8f, 1.2f), m);
            Prim(PrimitiveType.Cube, "Horn", v, new Vector3(0, 0.75f, 0.75f), new Vector3(0.2f, 0.2f, 0.5f), Mat("Bone", new Color(0.95f, 0.9f, 0.8f)));
            Prim(PrimitiveType.Cube, "Eye", v, new Vector3(0, 0.7f, 0.61f), new Vector3(0.5f, 0.12f, 0.05f), eyeMat);
        });
        var slime = BuildEnemyVariant("Enemy_Slime", 0.55f, v =>
        {
            var m = Mat("EnemySlime", new Color(0.35f, 0.85f, 0.4f));
            Prim(PrimitiveType.Sphere, "Body", v, new Vector3(0, 0.35f, 0), new Vector3(1.1f, 0.7f, 1.1f), m);
            Prim(PrimitiveType.Sphere, "Eye", v, new Vector3(0, 0.5f, 0.45f), new Vector3(0.3f, 0.2f, 0.1f), eyeMat);
        });
        var screamer = BuildEnemyVariant("Enemy_Screamer", 0.45f, v =>
        {
            var m = Mat("EnemyScreamer", new Color(0.55f, 0.3f, 0.85f));
            Prim(PrimitiveType.Capsule, "Body", v, new Vector3(0, 0.8f, 0), new Vector3(0.7f, 0.8f, 0.7f), m);
            Prim(PrimitiveType.Sphere, "Eye", v, new Vector3(0, 1.1f, 0.3f), new Vector3(0.45f, 0.45f, 0.15f), Mat("ScreamEye", new Color(1f, 0.95f, 0.6f)));
            Prim(PrimitiveType.Cube, "Mouth", v, new Vector3(0, 0.6f, 0.34f), new Vector3(0.35f, 0.18f, 0.05f), Mat("Mouth", new Color(0.1f, 0.05f, 0.1f)));
        });
        var leech = BuildEnemyVariant("Enemy_Leech", 0.35f, v =>
        {
            var m = Mat("EnemyLeech", new Color(0.55f, 0.08f, 0.12f));
            Prim(PrimitiveType.Cube, "Body", v, new Vector3(0, 0.3f, 0), new Vector3(0.55f, 0.45f, 0.8f), m);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, "Spike" + i, v, new Vector3((i % 2 == 0 ? -1 : 1) * 0.3f, 0.45f, i < 2 ? 0.2f : -0.2f), new Vector3(0.1f, 0.25f, 0.1f), m).transform.localRotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? 30 : -30));
            Prim(PrimitiveType.Cube, "Eye", v, new Vector3(0, 0.42f, 0.41f), new Vector3(0.3f, 0.08f, 0.04f), eyeMat);
        });

        AddEnemy(cfg, new EnemyDef { name = "졸개", prefab = cfg.enemyPrefab, weight = 3f });
        AddEnemy(cfg, new EnemyDef { name = "돌진꾼", prefab = charger, weight = 1.2f, hpMul = 1.4f, speedMul = 0.9f, damageMul = 1.2f,
            ability = EnemyAbility.Charge, abilityValue = 14f, abilityDuration = 0.6f, abilityCooldown = 5f, abilityRange = 7f });
        AddEnemy(cfg, new EnemyDef { name = "점액괴", prefab = slime, weight = 1f, hpMul = 1.8f, speedMul = 0.7f, damageMul = 0.8f,
            ability = EnemyAbility.Slow, abilityValue = 0.45f, abilityDuration = 3f });
        AddEnemy(cfg, new EnemyDef { name = "비명귀", prefab = screamer, weight = 0.8f, hpMul = 0.8f, speedMul = 1.1f, damageMul = 0.6f,
            ability = EnemyAbility.Fear, abilityDuration = 1f, abilityCooldown = 6f, abilityRange = 2.5f });
        AddEnemy(cfg, new EnemyDef { name = "흡혈충", prefab = leech, weight = 1f, hpMul = 0.6f, speedMul = 1.4f, damageMul = 0.5f,
            ability = EnemyAbility.Bleed, abilityValue = 3f, abilityDuration = 4f });

        // 프리팹 참조는 항상 최신으로
        foreach (var d in cfg.enemyTypes)
        {
            if (d.name == "돌진꾼") d.prefab = charger;
            else if (d.name == "점액괴") d.prefab = slime;
            else if (d.name == "비명귀") d.prefab = screamer;
            else if (d.name == "흡혈충") d.prefab = leech;
        }
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log("[PrototypeBuilder] 적 종류 업데이트 완료");
    }

    static void AddEnemy(GameConfig cfg, EnemyDef d)
    {
        if (cfg.enemyTypes == null) cfg.enemyTypes = new EnemyDef[0];
        if (System.Array.FindIndex(cfg.enemyTypes, x => x.name == d.name) < 0) ArrayUtility.Add(ref cfg.enemyTypes, d);
    }

    /// <summary>기본 적과 같은 구조(콜라이더/리지드바디/Visual)에 모양만 다르게.</summary>
    static Enemy BuildEnemyVariant(string name, float radius, System.Action<Transform> buildVisual)
    {
        var go = new GameObject(name) { layer = enemyLayer };
        var col = go.AddComponent<CapsuleCollider>(); col.radius = radius; col.height = 1f; col.center = new Vector3(0, 0.5f, 0);
        col.sharedMaterial = Frictionless();
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        var visual = new GameObject("Visual") { layer = enemyLayer }.transform; visual.SetParent(go.transform, false);
        buildVisual(visual);
        Set(go.AddComponent<Enemy>(), "visual", visual);
        return Save(go, name).GetComponent<Enemy>();
    }
}
