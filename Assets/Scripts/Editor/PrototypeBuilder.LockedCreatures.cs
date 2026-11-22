using UnityEditor;
using UnityEngine;

/// <summary>
/// 잠긴 생명체 2종 (게임 진행으로 해금): 새싹 요정(Sprout), 꽃 천사(Angel, 날개 뼈 + WingFlap).
/// Meshy FBX(T/A자, 리깅 없음) → CreatureRigBuilder 자동 리깅 → ENTITY/PLAYER/{Sprout,Angel}/ 프리팹 → WardConfig.creatures 등록(lockedByDefault).
/// 팔 위치 힌트 등 수치는 모델별로 측정한 값 (최종 키 0.9 기준 월드 단위). 메뉴: Roguelike/Build Sprout and Angel Creatures
/// </summary>
public static partial class PrototypeBuilder
{
    const string PlayerEntityDir = "Assets/Prototype/Creatures/ENTITY/PLAYER";
    const string SproutFbx = PlayerEntityDir + "/Meshy_AI_Blossom_Sprout_Sprite_20261116_texture_fbx/Meshy_AI_Blossom_Sprout_Sprite_20261116_texture_fbx/Meshy_AI_Blossom_Sprout_Sprite_20261116_texture.fbx";
    const string AngelFbx = PlayerEntityDir + "/Meshy_AI_Celestial_Blossom_Angel_20261116_texture_fbx/Meshy_AI_Celestial_Blossom_Angel_20261116_texture_fbx/Meshy_AI_Celestial_Blossom_Angel_20261116_texture.fbx";

    [MenuItem("Roguelike/Build Sprout and Angel Creatures")]
    public static void BuildSproutAndAngel()
    {
        // 새싹 요정: 팔이 내려간 A자, 머리 위 꽃이 정수리라 머리카락 분리는 끔 (잎 머리 = 소매와 같은 색)
        var sprout = RigLocked(SproutFbx, new CreatureRigBuilder.Settings
        {
            id = "Sprout", outDir = PlayerEntityDir + "/Sprout", separateHair = false,
            shoulderHint = new Vector2(0.075f, 0.31f), handHint = new Vector2(0.25f, 0.22f), armRadiusHint = 0.025f, armDown = 40f,
        });
        // 꽃 천사: T자, 등 뒤 날개(Wing_L/R) + 양옆으로 늘어진 리본은 다리에서 제외, 머리 위 고리라 머리카락 색은 머리 윗부분에서
        var angel = RigLocked(AngelFbx, new CreatureRigBuilder.Settings
        {
            id = "Angel", outDir = PlayerEntityDir + "/Angel", hairSampleY = 0.78f,
            shoulderHint = new Vector2(0.11f, 0.41f), handHint = new Vector2(0.287f, 0.408f), armRadiusHint = 0.035f, armDown = 65f,
            legOuterMax = 0.1f,
            wings = true, wingRootX = 0.13f, wingBackZ = -0.07f, wingMinY = 0.27f, wingBlend = 0.04f,
        });

        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        if (sprout) RegisterLocked(cfg.ward, "새싹 요정", "꽃잎 머리의 작은 요정. 풀숲에서 태어났어요", AssetDatabase.GetAssetPath(sprout));
        if (angel) RegisterLocked(cfg.ward, "꽃 천사", "꽃 날개를 단 천사. 하늘 정원을 지켜요", AssetDatabase.GetAssetPath(angel));
        EditorUtility.SetDirty(cfg.ward);
        AssetDatabase.SaveAssets();
    }

    static GameObject RigLocked(string fbx, CreatureRigBuilder.Settings s)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (!src) { Debug.LogWarning("모델 없음: " + fbx); return null; }
        var prefab = CreatureRigBuilder.Build(src, s, out string log);
        Debug.Log("[CreatureRig] " + log);
        return prefab;
    }

    static void RegisterLocked(WardConfig ward, string name, string desc, string modelPath)
    {
        RegisterCreature(ward, name, desc, modelPath);
        foreach (var c in ward.creatures)
            if (c != null && c.name == name) { c.lockedByDefault = true; c.tintStrength = 0.45f; }
    }
}
