using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.SceneManagement;

/// <summary>
/// 동반자 선택 플랫폼(Floor (1) / OuterWall (1) / CompanionStation)을 메인 로비 플랫폼에 붙인다.
/// 두 원형 플랫폼을 겹치게 옮기고, 겹치는 구간의 벽(콜라이더 포함)과 바닥을 잘라 통로를 연다.
///
/// - 로비 씬이 로드될 때마다 자동으로 확인하고, 아직 안 붙어 있으면 적용한다 (Play 시 항상 보장).
/// - 에디터에서는 PrototypeBuilder가 같은 함수로 씬 자체를 고쳐 저장한다.
/// 이미 붙어 있으면 아무것도 하지 않는다.
/// </summary>
public static class LobbyPlatformConnector
{
    /// <summary>로비 플랫폼 바닥 반지름 (ProBuilder 원판 기준).</summary>
    public const float PlatformRadius = 19.5f;
    /// <summary>메인 중심에서 동반자 플랫폼 중심까지 거리. 39면 가장자리만 닿고, 작을수록 넓게 겹친다.</summary>
    public const float Distance = 36f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Connect();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Connect();

    /// <summary>로비 씬이 열려 있고 아직 연결되지 않았으면 true.</summary>
    public static bool NeedsConnect()
    {
        var mainFloor = GameObject.Find("Lobby/Floor");
        var sideFloor = GameObject.Find("Lobby/Floor (1)");
        if (!mainFloor || !sideFloor) return false;
        float d = Vector3.Distance(Flat(mainFloor.transform.position), Flat(sideFloor.transform.position));
        return Mathf.Abs(d - Distance) > 0.01f;
    }

    /// <summary>연결 적용. 바꾼 게 있으면 요약 문구, 없으면 null.</summary>
    public static string Connect()
    {
        if (!NeedsConnect()) return null;

        var mainFloor = GameObject.Find("Lobby/Floor");
        var mainWall = GameObject.Find("Lobby/OuterWall");
        var sideFloor = GameObject.Find("Lobby/Floor (1)");
        var sideWall = GameObject.Find("Lobby/OuterWall (1)");
        var station = GameObject.Find("CompanionStation");

        // 1. 동반자 플랫폼을 메인 쪽으로 당겨 겹치게 (선택 장치/카메라/조명 포함)
        Vector3 mainCenter = Flat(mainFloor.transform.position);
        Vector3 target = mainCenter + Vector3.right * Distance;
        Vector3 delta = target - Flat(sideFloor.transform.position);
        sideFloor.transform.position += delta;
        if (sideWall) sideWall.transform.position += delta;
        if (station) station.transform.position += delta;

        // 겹친 바닥이 깜빡이지 않도록 동반자 바닥을 살짝 낮춤
        var fp = sideFloor.transform.position;
        fp.y = mainFloor.transform.position.y - 0.02f;
        sideFloor.transform.position = fp;

        // 2. 겹치는 구간의 벽/바닥 면 삭제 → 통로
        int cutMain = mainWall ? CutFacesInside(mainWall, target, PlatformRadius) : 0;
        int cutSide = sideWall ? CutFacesInside(sideWall, mainCenter, PlatformRadius) : 0;
        int cutFloor = CutFacesInside(sideFloor, mainCenter, PlatformRadius - 0.5f);

        // 3. 이음부를 막는 메인 쪽 기둥(Pillar/Base/Cap) 숨김
        int hidden = 0;
        foreach (Transform child in mainFloor.transform.parent)
        {
            if (child.name != "Pillar" && child.name != "Base" && child.name != "Cap") continue;
            if (!child.gameObject.activeSelf) continue;
            if (Vector3.Distance(Flat(child.position), target) < PlatformRadius + 1f)
            {
                child.gameObject.SetActive(false);
                hidden++;
            }
        }

        return $"동반자 플랫폼 연결: 중심 x={target.x:0.#}, 벽 면 삭제 {cutMain}+{cutSide}, 바닥 면 삭제 {cutFloor}, 기둥 숨김 {hidden}";
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    /// <summary>면 중심이 (center, radius) 원 안에 들어오는 ProBuilder 면을 삭제하고 메시/콜라이더 갱신.</summary>
    public static int CutFacesInside(GameObject go, Vector3 center, float radius)
    {
        var pb = go.GetComponent<ProBuilderMesh>();
        if (!pb) return 0;

        var positions = pb.positions;
        var remove = new List<Face>();
        foreach (var face in pb.faces)
        {
            var idx = face.distinctIndexes;
            Vector3 c = Vector3.zero;
            for (int i = 0; i < idx.Count; i++) c += positions[idx[i]];
            c = go.transform.TransformPoint(c / idx.Count);
            if (Vector3.Distance(Flat(c), center) < radius) remove.Add(face);
        }
        if (remove.Count == 0) return 0;

        pb.DeleteFaces(remove);
        pb.ToMesh();
        pb.Refresh();

        var mc = go.GetComponent<MeshCollider>();
        if (mc)
        {
            mc.sharedMesh = null;
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        }
        return remove.Count;
    }
}
