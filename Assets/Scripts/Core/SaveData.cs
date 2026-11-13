using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 런이 끝나도 남는 진행 상황 (자원, 거점 성장 레벨, 해금한 감시자). persistentDataPath/save.json.
/// </summary>
public static class SaveData
{
    [Serializable]
    class Data
    {
        public List<int> resources = new List<int>();          // ResourceType 인덱스별 보유량
        public List<string> upgradeIds = new List<string>();
        public List<int> upgradeLevels = new List<int>();
        public List<string> unlockedCompanions = new List<string>();
        public List<int> equippedSkills = new List<int>();     // 감시자 스킬 장착 칸 (WatcherSkillType, -1 = 빈 칸)
        public List<int> unlockedSkills = new List<int>();     // 기본 해금 외에 해금한 감시자 스킬
    }

    static Data data;
    static string PathFile => Path.Combine(Application.persistentDataPath, "save.json");

    /// <summary>자원 수가 바뀔 때 (획득/소비).</summary>
    public static event Action<ResourceType, int> ResourceChanged;

    static Data D
    {
        get
        {
            if (data != null) return data;
            try { data = File.Exists(PathFile) ? JsonUtility.FromJson<Data>(File.ReadAllText(PathFile)) : null; }
            catch (Exception e) { Debug.LogWarning("[SaveData] 불러오기 실패: " + e.Message); }
            return data ??= new Data();
        }
    }

    public static void Save()
    {
        try { File.WriteAllText(PathFile, JsonUtility.ToJson(D)); }
        catch (Exception e) { Debug.LogWarning("[SaveData] 저장 실패: " + e.Message); }
    }

    // ---- 자원 ----
    public static int Get(ResourceType t)
    {
        int i = (int)t;
        return i < D.resources.Count ? D.resources[i] : 0;
    }

    public static void Add(ResourceType t, int amount)
    {
        int i = (int)t;
        while (D.resources.Count <= i) D.resources.Add(0);
        D.resources[i] = Mathf.Max(0, D.resources[i] + amount);
        Save();
        ResourceChanged?.Invoke(t, D.resources[i]);
    }

    public static bool CanAfford(ResourceCost[] costs)
    {
        if (costs == null) return true;
        foreach (var c in costs) if (Get(c.type) < c.amount) return false;
        return true;
    }

    public static bool Spend(ResourceCost[] costs)
    {
        if (!CanAfford(costs)) return false;
        if (costs != null) foreach (var c in costs) Add(c.type, -c.amount);
        return true;
    }

    // ---- 거점 성장 ----
    public static int Level(string id)
    {
        int i = D.upgradeIds.IndexOf(id);
        return i >= 0 ? D.upgradeLevels[i] : 0;
    }

    public static void SetLevel(string id, int level)
    {
        int i = D.upgradeIds.IndexOf(id);
        if (i < 0) { D.upgradeIds.Add(id); D.upgradeLevels.Add(level); }
        else D.upgradeLevels[i] = level;
        Save();
    }

    // ---- 감시자 해금 ----
    public static bool IsUnlocked(CompanionDef c) =>
        DevConfig.UnlockAll || c.unlockCost == null || c.unlockCost.Length == 0 || D.unlockedCompanions.Contains(c.name);

    public static void Unlock(CompanionDef c)
    {
        if (!D.unlockedCompanions.Contains(c.name)) D.unlockedCompanions.Add(c.name);
        Save();
    }

    // ---- 감시자 스킬 ----
    /// <summary>장착 칸 i의 스킬 (-1 = 빈 칸).</summary>
    public static int EquippedSkill(int slot) => slot < D.equippedSkills.Count ? D.equippedSkills[slot] : -1;

    public static void SetEquippedSkill(int slot, int skill)
    {
        while (D.equippedSkills.Count <= slot) D.equippedSkills.Add(-1);
        D.equippedSkills[slot] = skill;
        Save();
    }

    /// <summary>처음(장착 기록이 없을 때) 기본 장착: 1번 폭격(기존 R 폭격을 이어받음), 2번 디코이.</summary>
    public static void EnsureSkillLoadout()
    {
        if (D.equippedSkills.Count > 0) return;
        D.equippedSkills.Add((int)WatcherSkillType.Bombard);
        D.equippedSkills.Add((int)WatcherSkillType.Decoy);
        Save();
    }

    public static bool SkillUnlocked(WatcherSkillDef d) => d != null && (d.unlockedByDefault || D.unlockedSkills.Contains((int)d.type));
    public static void UnlockSkill(WatcherSkillType t) { if (!D.unlockedSkills.Contains((int)t)) { D.unlockedSkills.Add((int)t); Save(); } }

    /// <summary>스킬 성장 레벨 (별도 성장 화면에서 올림 — 아직 미구현). 거점 성장 레벨 저장소를 같이 씀.</summary>
    public static int SkillLevel(WatcherSkillType t) => Level("wskill_" + t);
    public static void SetSkillLevel(WatcherSkillType t, int level) => SetLevel("wskill_" + t, level);
}
