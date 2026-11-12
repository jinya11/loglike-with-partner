using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>묘지 파생 씬: 깨어난 묘지 (기본형 묘지 지형 그대로 + 부술 수 있는 무덤).</summary>
public static partial class PrototypeBuilder
{
    static void BuildAwakenedGraveyard(GameObject root, AwakenedGraveyardSettings s)
    {
        root.AddComponent<AwakenedGraveyardVariant>();
        var prefab = GraveMoundPrefab(s);
        var parent = new GameObject("Graves").transform;
        parent.SetParent(root.transform, false);
        var t = Terrain.activeTerrain;
        var rng = new System.Random(31);
        var used = new List<Vector3>();
        float lim = RegionHalf * 0.78f;
        for (int tries = 0; used.Count < s.graveCount && tries < 900; tries++)
        {
            var p = new Vector3(((float)rng.NextDouble() * 2f - 1f) * lim, 0f, ((float)rng.NextDouble() * 2f - 1f) * lim);
            if (p.magnitude < 14f) continue; // 시작 지점 근처는 비움
            if (t)
            {
                var td = t.terrainData; var tp = t.transform.position;
                if (td.GetSteepness((p.x - tp.x) / td.size.x, (p.z - tp.z) / td.size.z) > 16f) continue; // 비탈엔 안 놓음
            }
            bool near = false;
            foreach (var u in used) if ((u - p).sqrMagnitude < 20f * 20f) { near = true; break; }
            if (near && tries < 700) continue;
            used.Add(p);
            p.y = SampleGround(t, p.x, p.z);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = $"Grave_{used.Count:00}";
            go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
        }
    }

    /// <summary>무덤: 흙무덤 + 비석(기울어짐) / 부서진 잔해. Enemy 레이어 트리거 충돌체 (탄환/범위 공격 판정).</summary>
    static GameObject GraveMoundPrefab(AwakenedGraveyardSettings s)
    {
        string path = $"{VariantPrefabDir}/GraveMound.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var stoneMat = ClayMat("GraveStone", s.stoneColor);
        var moundMat = ClayMat("GraveMound", s.moundColor);
        var root = new GameObject("GraveMound");
        root.transform.localScale = Vector3.one * 1.5f; // 멀리 있는 카메라에서도 잘 보이게
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) root.layer = enemyLayer;
        var col = root.AddComponent<CapsuleCollider>();
        col.isTrigger = true; col.radius = 1.1f; col.height = 2.4f; col.center = new Vector3(0f, 0.9f, 0f);
        var target = root.AddComponent<StageTarget>();
        target.aimHeight = 0.7f; target.hitColor = s.stoneColor;
        var grave = root.AddComponent<GraveMound>();

        var intact = new GameObject("Intact");
        intact.transform.SetParent(root.transform, false);
        LPart(PrimitiveType.Sphere, "Mound", intact.transform, new Vector3(0f, 0f, -0.5f), new Vector3(1.5f, 0.55f, 2.3f), moundMat);
        var stone = new GameObject("Stone").transform;
        stone.SetParent(intact.transform, false);
        stone.localPosition = new Vector3(0f, 0f, 0.75f);
        LPart(PrimitiveType.Cube, "Slab", stone, new Vector3(0f, 0.6f, 0f), new Vector3(0.95f, 1.2f, 0.22f), stoneMat);
        LPart(PrimitiveType.Cylinder, "Top", stone, new Vector3(0f, 1.2f, 0f), new Vector3(0.95f, 0.11f, 0.95f), stoneMat).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        LPart(PrimitiveType.Cube, "CrossV", stone, new Vector3(0f, 0.75f, -0.12f), new Vector3(0.1f, 0.5f, 0.03f), moundMat);
        LPart(PrimitiveType.Cube, "CrossH", stone, new Vector3(0f, 0.85f, -0.12f), new Vector3(0.34f, 0.09f, 0.03f), moundMat);
        grave.stone = stone;
        grave.intact = intact;

        var rubble = new GameObject("Rubble");
        rubble.transform.SetParent(root.transform, false);
        LPart(PrimitiveType.Sphere, "OpenPit", rubble.transform, new Vector3(0f, -0.05f, -0.4f), new Vector3(1.6f, 0.2f, 2.2f), moundMat);
        var fallen = LPart(PrimitiveType.Cube, "FallenSlab", rubble.transform, new Vector3(0.35f, 0.12f, 0.9f), new Vector3(0.95f, 0.22f, 1.1f), stoneMat);
        fallen.transform.localRotation = Quaternion.Euler(0f, 25f, 8f);
        LPart(PrimitiveType.Cube, "Chip1", rubble.transform, new Vector3(-0.6f, 0.08f, 1.1f), new Vector3(0.3f, 0.16f, 0.25f), stoneMat).transform.localRotation = Quaternion.Euler(10f, 40f, 0f);
        LPart(PrimitiveType.Cube, "Chip2", rubble.transform, new Vector3(0.8f, 0.07f, 0.2f), new Vector3(0.22f, 0.14f, 0.2f), stoneMat).transform.localRotation = Quaternion.Euler(0f, 70f, 12f);
        rubble.SetActive(false);
        grave.rubble = rubble;

        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }
}
