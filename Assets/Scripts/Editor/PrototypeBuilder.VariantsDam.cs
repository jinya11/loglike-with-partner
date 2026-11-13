using UnityEditor;
using UnityEngine;

/// <summary>강 파생 씬: 상류의 댐 (지형·보더 없음, 세로로 긴 강바닥 발판 + 둘레 강물 + 위쪽 끝 댐 벽과 수문 장치 + 물살).</summary>
public static partial class PrototypeBuilder
{
    static void BuildRiverDam(GameObject root, RiverDamSettings s)
    {
        var rule = root.AddComponent<RiverDamVariant>();
        float halfW = s.deckWidth * 0.5f, halfL = s.deckLength * 0.5f, gateZ = halfL - s.gateInset;

        // 지형/보더/바닥 구역 없음, 경계는 원형(세로 길이 반경) — 발판 밖은 강물
        foreach (var t in Object.FindObjectsByType<Terrain>()) Object.DestroyImmediate(t.gameObject);
        var border = GameObject.Find("Arena/Border");
        if (border) Object.DestroyImmediate(border);
        foreach (var z in Object.FindObjectsByType<StageZone>()) Object.DestroyImmediate(z.gameObject);
        var gen = Object.FindAnyObjectByType<StageGenerator>();
        if (gen)
        {
            var so = new SerializedObject(gen);
            so.FindProperty("border").objectReferenceValue = null;
            so.FindProperty("radius").floatValue = halfL + 6f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player) { var p = player.transform.position; player.transform.position = new Vector3(0f, p.y, gateZ - 9f); }

        var deckA = ClayMat("DamDeck", s.deckColor);
        var deckB = ClayMat("DamDeck2", s.deckColor * 0.92f);
        var waterMat = ClayMat("DamWater", s.waterColor);
        waterMat.SetFloat("_Smoothness", 0.5f); // 물만 살짝 반짝
        var wallMat = ClayMat("DamWall", s.wallColor);

        // 강바닥 발판 (윗면 = 0)
        var deck = new GameObject("Deck").transform;
        deck.SetParent(root.transform, false);
        int nx = Mathf.Max(1, Mathf.RoundToInt(s.deckWidth / s.slabSize)), nz = Mathf.Max(1, Mathf.RoundToInt(s.deckLength / s.slabSize));
        float sx = s.deckWidth / nx, sz = s.deckLength / nz;
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                var c = new Vector3(-halfW + sx * (ix + 0.5f), -0.6f, -halfL + sz * (iz + 0.5f));
                LPart(PrimitiveType.Cube, $"Slab_{ix}_{iz}", deck, c, new Vector3(sx - 0.12f, 1.2f, sz - 0.12f), (ix + iz) % 2 == 0 ? deckA : deckB);
            }
        // 둘레 강물 (넓게)
        var water = LPart(PrimitiveType.Cube, "River", root.transform, new Vector3(0f, -0.9f, 0f), new Vector3(s.deckWidth * 6f, 0.2f, s.deckLength * 2.2f), waterMat);
        water.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 위쪽 끝 댐 벽
        LPart(PrimitiveType.Cube, "DamWall", root.transform, new Vector3(0f, 4f, halfL + 3f), new Vector3(s.deckWidth + 24f, 10f, 6f), wallMat);
        LPart(PrimitiveType.Cube, "DamCrest", root.transform, new Vector3(0f, 9.4f, halfL + 3f), new Vector3(s.deckWidth + 26f, 0.8f, 7f), deckA);

        // 수문 장치
        var gatePrefab = SluiceGatePrefab(s);
        var gates = new GameObject("SluiceGates").transform;
        gates.SetParent(root.transform, false);
        int n = Mathf.Max(1, s.gateCount);
        for (int i = 0; i < n; i++)
        {
            float x = n == 1 ? 0f : Mathf.Lerp(-halfW + 5f, halfW - 5f, i / (float)(n - 1));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab, gates);
            go.name = $"SluiceGate_{i + 1}";
            go.transform.SetPositionAndRotation(new Vector3(x, 0f, gateZ), Quaternion.identity);
        }

        // 물살 (꺼 둠 — 수문이 열리면 복제되어 내려감)
        var wave = new GameObject("FloodWave").transform;
        wave.SetParent(root.transform, false);
        LPart(PrimitiveType.Cube, "Water", wave, new Vector3(0f, 0.6f, 0f), new Vector3(1f, 1.2f, 3f), waterMat).shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        LPart(PrimitiveType.Cube, "Foam", wave, new Vector3(0f, 1.25f, -1.2f), new Vector3(1f, 0.25f, 0.8f), ClayMat("DamFoam", new Color(0.92f, 0.97f, 1f))).shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        wave.gameObject.SetActive(false);
        rule.floodWave = wave;
    }

    /// <summary>수문 장치: 돌 기둥 두 개 + 윗보 + 나무 문짝(열리면 올라감) + 핸들 바퀴 + 바닥 차오름 고리 + 체력 막대 / 부서진 잔해.</summary>
    static GameObject SluiceGatePrefab(RiverDamSettings s)
    {
        string path = $"{VariantPrefabDir}/SluiceGate.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var stone = ClayMat("DamWall", s.wallColor);
        var wood = ClayMat("SluiceWood", s.gateColor);
        var ready = VMat("SluiceReady", "Universal Render Pipeline/Unlit", s.readyColor * 1.3f);
        var charging = VMat("SluiceCharging", "Universal Render Pipeline/Unlit", s.readyColor * 0.35f);
        var hpMat = VMat("SluiceHp", "Universal Render Pipeline/Unlit", new Color(0.55f, 0.95f, 0.55f));
        var hpBack = VMat("SluiceHpBack", "Universal Render Pipeline/Unlit", new Color(0.12f, 0.12f, 0.14f));
        var root = new GameObject("SluiceGate");
        var g = root.AddComponent<SluiceGate>();
        var intact = new GameObject("Intact").transform;
        intact.SetParent(root.transform, false);
        LPart(PrimitiveType.Cube, "PillarL", intact, new Vector3(-1.7f, 1.6f, 0f), new Vector3(0.8f, 3.2f, 1.2f), stone);
        LPart(PrimitiveType.Cube, "PillarR", intact, new Vector3(1.7f, 1.6f, 0f), new Vector3(0.8f, 3.2f, 1.2f), stone);
        LPart(PrimitiveType.Cube, "Lintel", intact, new Vector3(0f, 3.4f, 0f), new Vector3(4.4f, 0.6f, 1.3f), stone);
        var door = new GameObject("Door").transform;
        door.SetParent(intact, false);
        LPart(PrimitiveType.Cube, "Panel", door, new Vector3(0f, 1.4f, 0f), new Vector3(2.6f, 2.8f, 0.3f), wood);
        for (int i = 0; i < 3; i++) LPart(PrimitiveType.Cube, "Plank", door, new Vector3(0f, 0.5f + i * 0.9f, -0.17f), new Vector3(2.6f, 0.12f, 0.06f), stone);
        g.door = door;
        var wheel = LPart(PrimitiveType.Cylinder, "Wheel", intact, new Vector3(2.4f, 2.2f, -0.2f), new Vector3(1.1f, 0.08f, 1.1f), wood);
        wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        g.intactLook = intact.gameObject;
        var broken = new GameObject("Broken");
        broken.transform.SetParent(root.transform, false);
        LPart(PrimitiveType.Cube, "Stump", broken.transform, new Vector3(-1.7f, 0.5f, 0f), new Vector3(0.9f, 1f, 1.2f), stone);
        LPart(PrimitiveType.Cube, "FallenDoor", broken.transform, new Vector3(0.2f, 0.18f, -1f), new Vector3(2.6f, 0.3f, 1.8f), wood).transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
        broken.SetActive(false);
        g.brokenLook = broken;
        var mark = LPart(PrimitiveType.Cylinder, "ChargeMark", root.transform, new Vector3(0f, 0.03f, -2.2f), new Vector3(3f, 0.02f, 3f), charging);
        mark.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.chargeMark = mark.transform; g.chargeRenderer = mark; g.readyMaterial = ready; g.chargingMaterial = charging;
        LPart(PrimitiveType.Cube, "HpBack", root.transform, new Vector3(0f, 4.4f, 0f), new Vector3(3.2f, 0.28f, 0.1f), hpBack).shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var hp = LPart(PrimitiveType.Cube, "HpBar", root.transform, new Vector3(0f, 4.4f, -0.06f), new Vector3(3f, 0.2f, 0.1f), hpMat);
        hp.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.hpBar = hp.transform;
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }
}
