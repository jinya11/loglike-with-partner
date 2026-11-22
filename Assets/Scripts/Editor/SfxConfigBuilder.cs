using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SfxConfig의 비어 있는 클립만 Assets/Audio/Sfx 파일로 채움 (이미 넣은 클립/음량은 건드리지 않음).
/// 일반 효과음: 필드 이름의 스네이크 표기 (enemyHit → enemy_hit). 생명체: 접두어_종류 (rabbit_impact, 없으면 creature_hurt 등 공용).
/// </summary>
public static class SfxConfigBuilder
{
    const string AssetPath = "Assets/Prototype/Resources/Config/SfxConfig.asset";
    const string AudioRoot = "Assets/Audio/Sfx";
    // WardConfig 생명체 이름 → 파일 접두어
    static readonly (string name, string prefix)[] CreatureFiles = { ("토끼", "rabbit"), ("마녀", "witch"), ("새 소녀", "bird") };

    [MenuItem("Roguelike/Build Sfx Config")]
    public static void Build()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<SfxConfig>(AssetPath);
        if (!cfg) { cfg = ScriptableObject.CreateInstance<SfxConfig>(); AssetDatabase.CreateAsset(cfg, AssetPath); }

        var clips = new Dictionary<string, AudioClip>();
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string key = System.IO.Path.GetFileNameWithoutExtension(path);
            clips[key] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Import(path, key == "wind" || key == "sandstorm");
        }

        foreach (var f in typeof(SfxConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (f.FieldType == typeof(Sfx)) Fill((Sfx)f.GetValue(cfg), clips, Snake(f.Name));

        var list = new List<CreatureSfx>(cfg.creatures ?? new CreatureSfx[0]);
        foreach (var (name, prefix) in CreatureFiles)
        {
            var c = cfg.Creature(name);
            if (c == null) { c = new CreatureSfx { creature = name }; list.Add(c); cfg.creatures = list.ToArray(); }
            Fill(c.attack, clips, prefix + "_attack");
            Fill(c.impact, clips, prefix + "_impact");
            Fill(c.skill, clips, prefix + "_skill", "creature_skill");
            Fill(c.hurt, clips, prefix + "_hurt", "creature_hurt");
            Fill(c.die, clips, prefix + "_die", "creature_die");
        }
        cfg.creatures = list.ToArray();
        EditorUtility.SetDirty(cfg);

        var game = Resources.Load<GameConfig>("GameConfig");
        if (game && game.sfx != cfg) { game.sfx = cfg; EditorUtility.SetDirty(game); }
        AssetDatabase.SaveAssets();
        Debug.Log("[Sfx] SfxConfig 갱신");
    }

    static void Fill(Sfx s, Dictionary<string, AudioClip> clips, params string[] keys)
    {
        if (s == null || s.clip) return;
        foreach (var k in keys) if (clips.TryGetValue(k, out var c)) { s.clip = c; return; }
    }

    static string Snake(string s) => Regex.Replace(s, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();

    static void Import(string path, bool longClip)
    {
        if (AssetImporter.GetAtPath(path) is not AudioImporter imp) return;
        var st = imp.defaultSampleSettings;
        var want = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        if (st.loadType == want && !imp.loadInBackground) return;
        st.loadType = want; st.compressionFormat = AudioCompressionFormat.Vorbis; st.quality = 0.7f;
        imp.defaultSampleSettings = st; imp.loadInBackground = false;
        imp.SaveAndReimport();
    }
}
