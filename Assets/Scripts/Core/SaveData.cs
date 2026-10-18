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
        c.unlockCost == null || c.unlockCost.Length == 0 || D.unlockedCompanions.Contains(c.name);

    public static void Unlock(CompanionDef c)
    {
        if (!D.unlockedCompanions.Contains(c.name)) D.unlockedCompanions.Add(c.name);
        Save();
    }
}
