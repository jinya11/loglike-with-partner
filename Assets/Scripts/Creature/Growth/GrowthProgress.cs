using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 생명체 성장 진행도 싱글톤 (해금 노드, 선택한 메인 노드, 성장 재화). 저장은 기존 SaveData(save.json)에 함께.
/// 해금 여부는 HashSet으로 캐시 (전투 중 조회 비용 최소).
/// 성장 재화 = ResourceType.GrowthSeed (SaveData 자원). 획득: 로비 교환소에서 거점 재화를 교환 (ExchangeUI).
/// </summary>
public sealed class GrowthProgress
{
    static GrowthProgress inst;
    public static GrowthProgress I => inst ??= new GrowthProgress();
    /// <summary>저장 삭제(새 게임) 후 캐시 버림 — 다음 접근 때 다시 읽음.</summary>
    public static void ResetCache() => inst = null;

    public const ResourceType Currency = ResourceType.GrowthSeed;

    readonly HashSet<string> owned;
    readonly Dictionary<string, string> mains = new Dictionary<string, string>();

    /// <summary>해금/선택/재화가 바뀔 때.</summary>
    public event System.Action Changed;

    GrowthProgress()
    {
        owned = new HashSet<string>(SaveData.GrowthNodes);
        foreach (var m in SaveData.GrowthMains)
        {
            int k = m.IndexOf('/');
            if (k > 0) mains[m.Substring(0, k)] = m.Substring(k + 1);
        }

    }

    static string Key(CreatureGrowthConfig c, string id) => c.creatureName + "/" + id;

    public int Seeds => SaveData.Get(Currency);
    public bool Owned(CreatureGrowthConfig c, GrowthNodeDef n) => c && n != null && Has(Key(c, n.id));

    /// <summary>해금 여부 (개발자 F2 스킬 전체 해금 중이면 전부 해금으로 취급, 저장은 그대로).</summary>
    bool Has(string key) => DevConfig.UnlockAllSkills || owned.Contains(key);

    /// <summary>재화/해금 상태가 바뀌었음을 알림 (교환소, 개발자 단축키).</summary>
    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>선행 노드 중 하나라도 해금 (선행이 없으면 시작 노드).</summary>
    public bool Reachable(CreatureGrowthConfig c, GrowthNodeDef n)
    {
        if (n.requires == null || n.requires.Length == 0) return true;
        foreach (var r in n.requires) if (Has(Key(c, r))) return true;
        return false;
    }

    public bool CanUnlock(CreatureGrowthConfig c, GrowthNodeDef n) => !Owned(c, n) && Reachable(c, n) && ConditionMet(c, n) && Seeds >= n.cost;

    // ---- 클리어 조건 (예: 마법사로 1회 클리어) ----
    static string ClearKey(CreatureGrowthConfig c) => "clear_" + c.creatureName;
    /// <summary>이 생명체로 클리어한 횟수 (거점 성장 레벨 저장소를 같이 씀).</summary>
    public int Clears(CreatureGrowthConfig c) => c ? SaveData.Level(ClearKey(c)) : 0;
    public bool ConditionMet(CreatureGrowthConfig c, GrowthNodeDef n) => n.requiredClears <= 0 || DevConfig.UnlockAllSkills || Clears(c) >= n.requiredClears;
    /// <summary>런 클리어 (GameManager 승리 시).</summary>
    public void RecordClear(CreatureGrowthConfig c) { if (c) { SaveData.SetLevel(ClearKey(c), Clears(c) + 1); Changed?.Invoke(); } }

    public bool Unlock(CreatureGrowthConfig c, GrowthNodeDef n)
    {
        if (!CanUnlock(c, n)) return false;
        SaveData.Add(Currency, -n.cost);
        string k = Key(c, n.id);
        owned.Add(k); SaveData.GrowthNodes.Add(k);
        if (n.kind == GrowthNodeKind.Main && SelectedMain(c) == null) SetMain(c, n.id); // 첫 메인은 자동 선택 (저장 포함)
        else SaveData.Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>선택한 메인 노드 id (없으면 null = 기본 평타).</summary>
    public string SelectedMain(CreatureGrowthConfig c) => c && mains.TryGetValue(c.creatureName, out var id) ? id : null;

    /// <summary>메인 노드 선택/해제 (id = null이면 해제). 서로 배타: 생명체당 하나.</summary>
    public void SetMain(CreatureGrowthConfig c, string id)
    {
        if (!c) return;
        var list = SaveData.GrowthMains;
        string prefix = c.creatureName + "/";
        list.RemoveAll(s => s.StartsWith(prefix));
        if (string.IsNullOrEmpty(id)) mains.Remove(c.creatureName);
        else { mains[c.creatureName] = id; list.Add(prefix + id); }
        SaveData.Save();
        Changed?.Invoke();
    }

    /// <summary>해금한 Stat 노드를 런 시작 능력치에 적용 (GameManager.ResetRun).</summary>
    public void ApplyStats(CreatureGrowthConfig c, PlayerStats s)
    {
        if (!c || c.nodes == null) return;
        var u = new UpgradeDef();
        foreach (var n in c.nodes)
        {
            if (n == null || n.kind != GrowthNodeKind.Stat || !Has(Key(c, n.id))) continue;
            u.stat = n.stat; u.value = n.value;
            s.Apply(u);
        }
    }

    /// <summary>전투용 스냅샷: 선택한 메인 + 해금한 효과 (공격 시 매번 조회하지 않도록).</summary>
    public GrowthLoadout Loadout(CreatureGrowthConfig c)
    {
        var l = new GrowthLoadout { config = c };
        if (!c || c.nodes == null) return l;
        string main = SelectedMain(c);
        foreach (var n in c.nodes)
        {
            if (n == null || n.effect == GrowthEffect.None || !Has(Key(c, n.id))) continue;
            if (n.kind == GrowthNodeKind.Main) { if (n.id == main) l.main = n.effect; }
            else l.Set(n.effect);
        }
        return l;
    }
}

/// <summary>한 런 동안의 성장 효과 (전투 코드가 읽음).</summary>
public sealed class GrowthLoadout
{
    public CreatureGrowthConfig config;
    public GrowthEffect main;
    public bool bounce, skewer, lifesteal, carrotRain;
    public bool arcaneAll, arcaneOrbs; // 마법사
    public bool momentum, featherDrop, speedGuard, birdGust; // 새 소녀

    public bool Any => main != GrowthEffect.None || bounce || skewer || lifesteal || carrotRain || arcaneAll || arcaneOrbs || momentum || featherDrop || speedGuard || birdGust;

    /// <summary>마법사 메인 효과가 켜져 있는지 (장착한 메인, 또는 '모든 효과' 패시브).</summary>
    public bool Arcane(GrowthEffect e) => arcaneAll || main == e;

    public void Set(GrowthEffect e)
    {
        switch (e)
        {
            case GrowthEffect.BounceCarrot: bounce = true; break;
            case GrowthEffect.CarrotSkewer: skewer = true; break;
            case GrowthEffect.Lifesteal: lifesteal = true; break;
            case GrowthEffect.CarrotRain: carrotRain = true; break;
            case GrowthEffect.ArcaneAll: arcaneAll = true; break;
            case GrowthEffect.ArcaneOrbs: arcaneOrbs = true; break;
            case GrowthEffect.Momentum: momentum = true; break;
            case GrowthEffect.FeatherDrop: featherDrop = true; break;
            case GrowthEffect.SpeedGuard: speedGuard = true; break;
            case GrowthEffect.BirdGust: birdGust = true; break;
        }
    }
}
