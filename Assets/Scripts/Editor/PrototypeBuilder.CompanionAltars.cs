using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

/// <summary>
/// 동반자 제단(거신, 풍신 …)을 "동반자 선택 장소"(CompanionStation) 플랫폼에 모두 배치.
/// 메인 플랫폼에 있던 기존 동반자 제단은 지우고, GameConfig.companions 전체를 다시 만든다.
/// 제단에서 F → 그 동반자로 바로 교체. 가운데(CompanionStation)에서 F → 상세 설명 화면.
/// 이미 배치돼 있으면 아무것도 하지 않는다.
/// </summary>
public static partial class PrototypeBuilder
{
    /// <summary>동반자 플랫폼 중심 기준 제단 줄의 위치/간격 (쇼케이스 카메라 뒤쪽 = -Z).</summary>
    static readonly Vector3 AltarRowOffset = new Vector3(1f, 0f, -6f);
    const float AltarSpacing = 6f;

    /// <summary>바꾼 게 있으면 true.</summary>
    static bool ArrangeCompanionAltars()
    {
        var station = GameObject.Find("CompanionStation");
        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        if (!station || !cfg || cfg.companions == null || cfg.companions.Length == 0) return false;

        // 씬 안의 기존 동반자 제단 수집
        var existing = new List<LobbyInteractable>();
        foreach (var root in station.scene.GetRootGameObjects())
            foreach (var li in root.GetComponentsInChildren<LobbyInteractable>(true))
                if (li.kind == LobbyInteractable.Kind.Companion) existing.Add(li);

        // 이미 전부 동반자 플랫폼에 있으면 끝
        var seen = new HashSet<int>();
        bool done = existing.Count == cfg.companions.Length;
        foreach (var li in existing)
        {
            if (li.transform.parent != station.transform) done = false;
            seen.Add(li.index);
        }
        if (done && seen.Count == cfg.companions.Length) return false;

        foreach (var li in existing) Object.DestroyImmediate(li.gameObject);

        var stone = Mat("LobbyStone", new Color(0.33f, 0.31f, 0.36f));
        var rune = Mat("Rune", Color.white);
        var eye = Mat("CompanionEye", Color.white);
        int n = cfg.companions.Length;
        for (int i = 0; i < n; i++)
        {
            var def = cfg.companions[i];
            var t = new GameObject("Companion_" + def.name).transform;
            t.SetParent(station.transform, false);
            t.localPosition = AltarRowOffset + new Vector3((i - (n - 1) * 0.5f) * AltarSpacing, 0f, 0f);

            PB(ShapeGenerator.GenerateCylinder(PivotLocation.Center, 24, 2.6f, 0.25f, 0), "Altar", t, new Vector3(0, 0.125f, 0), stone, true);
            var ring = PB(ShapeGenerator.GeneratePipe(PivotLocation.Center, 2.6f, 0.04f, 0.2f, 24, 0), "Ring", t, new Vector3(0, 0.27f, 0), rune, false);
            var arch = PB(ShapeGenerator.GenerateArch(PivotLocation.Center, 180f, 3.2f, 0.6f, 0.8f, 12, true, true, true, true, true), "Arch", t, new Vector3(1.8f, 1.6f, 0), stone, false);
            arch.transform.localRotation = Quaternion.Euler(0, 90, 0);

            // 작은 조각상: 동반자별 색 (풍신은 부채 날개 추가)
            bool wind = def.name == "풍신";
            var body = wind ? Mat("WindBody", new Color(0.16f, 0.32f, 0.34f)) : Mat("Companion", new Color(0.18f, 0.16f, 0.25f));
            Prim(PrimitiveType.Cylinder, "MiniTorso", t, new Vector3(0.8f, 1.2f, 0), new Vector3(1f, 0.8f, 0.7f), body);
            Prim(PrimitiveType.Sphere, "MiniHead", t, new Vector3(0.8f, 2.4f, 0), new Vector3(0.8f, 0.85f, 0.8f), body);
            Prim(PrimitiveType.Sphere, "MiniEyeL", t, new Vector3(0.5f, 2.45f, -0.18f), new Vector3(0.12f, 0.1f, 0.1f), eye);
            Prim(PrimitiveType.Sphere, "MiniEyeR", t, new Vector3(0.5f, 2.45f, 0.18f), new Vector3(0.12f, 0.1f, 0.1f), eye);
            if (wind)
            {
                var fan = Mat("WindFan", new Color(0.55f, 0.9f, 0.85f));
                for (int s = -1; s <= 1; s += 2)
                {
                    var w = Prim(PrimitiveType.Cylinder, s < 0 ? "MiniFanL" : "MiniFanR", t, new Vector3(0.9f, 1.6f, s * 0.95f), new Vector3(0.9f, 0.02f, 0.9f), fan);
                    w.transform.localRotation = Quaternion.Euler(s * -65f, 0, 0);
                }
            }

            Interactable(t.gameObject, LobbyInteractable.Kind.Companion, i, ring.GetComponent<Renderer>(), new Color(0.75f, 0.6f, 1f));
        }
        Debug.Log($"[PrototypeBuilder] 동반자 제단 {n}개를 동반자 선택 장소로 배치");
        return true;
    }
}
