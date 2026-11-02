using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지역 스테이지의 보더: 지나갈 수 없는 얇은 에너지 장막 (셰이더 Roguelike/ArenaBorder).
/// 필드는 사각형 구역들의 합집합. 처음엔 중앙 정사각형 하나 → 보더를 열면(OpenSide) 그 변 전체 길이에
/// openDepth 깊이의 새 구역이 붙고, 보더는 구역들을 합친 바깥 외곽선을 따라감. 새 구역은 붙은 변에서부터 늘어나며 열림.
/// 열린 구역은 지역(씬)별로 저장되어 다음에 와도 유지. 충돌 벽은 Ignore Raycast 레이어 (길찾기 장애물 검사 제외).
/// </summary>
[ExecuteAlways]
public class ArenaBorder : MonoBehaviour
{
    [Tooltip("중앙 정사각형의 반 변")]
    [SerializeField] float halfSize = 60f;
    [Tooltip("보더를 열 때 붙는 새 구역의 깊이 (변 길이는 연 변 전체)")]
    [SerializeField] float openDepth = 60f;
    [Tooltip("최대로 열 수 있는 횟수")]
    [SerializeField] int maxOpens = 6;
    [Tooltip("중앙에서 이 거리를 넘어가는 구역은 열 수 없음")]
    [SerializeField] float maxReach = 240f;
    [SerializeField] float height = 3.5f;
    [Tooltip("충돌 벽 두께 (보더 바깥쪽으로)")]
    [SerializeField] float colliderThickness = 2f;
    [SerializeField] Material material;
    [Tooltip("열린 구역을 지역별로 저장 (끄면 매번 중앙만)")]
    [SerializeField] bool persist = true;

    public static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left };

    readonly List<Rect> areas = new List<Rect>(16);
    readonly List<Transform> walls = new List<Transform>(32);
    readonly List<Rect> rectBuf = new List<Rect>(16);
    readonly List<Vector4> edgeBuf = new List<Vector4>(64);
    readonly List<Vector2> spans = new List<Vector2>(8), spanTmp = new List<Vector2>(8);
    // 열리는 중인 구역 (붙은 변에서부터 늘어남)
    int growIndex = -1;
    Vector2Int growDir;
    float growT = 1f, growTime = 2f;
    bool dirty = true, loaded;

    public float HalfSize => halfSize;
    public IReadOnlyList<Rect> Areas => areas;
    /// <summary>중앙 외에 열린 구역 수.</summary>
    public int OpenCount => Mathf.Max(0, areas.Count - 1);
    public bool Growing => growT < 1f;
    /// <summary>새 구역이 다 열렸을 때 (구역 사각형, XZ).</summary>
    public event System.Action<Rect> AreaOpened;

    string SaveKey => "border_areas_" + gameObject.scene.name;
    Rect BaseRect => new Rect(-halfSize, -halfSize, halfSize * 2f, halfSize * 2f);

    void OnEnable() { dirty = true; Apply(); }
    void OnValidate() { halfSize = Mathf.Max(5f, halfSize); openDepth = Mathf.Max(2f, openDepth); dirty = true; loaded = false; }

    void Update()
    {
        if (growT < 1f)
        {
            growT = Mathf.Min(1f, growT + Time.deltaTime / growTime);
            dirty = true;
            if (growT >= 1f) { Save(); AreaOpened?.Invoke(areas[growIndex]); }
        }
        if (dirty) Apply();
    }

    /// <summary>점이 들어 있는 구역 번호 (-1: 밖).</summary>
    public int AreaAt(Vector3 p)
    {
        for (int i = 0; i < areas.Count; i++)
            if (areas[i].Contains(new Vector2(p.x, p.z))) return i;
        return -1;
    }

    public Rect AreaRect(int i) => areas[i];

    /// <summary>area의 dir 변에 붙을 새 구역.</summary>
    public Rect NewAreaRect(int area, Vector2Int dir)
    {
        var r = areas[area];
        if (dir == Vector2Int.up) return new Rect(r.xMin, r.yMax, r.width, openDepth);
        if (dir == Vector2Int.down) return new Rect(r.xMin, r.yMin - openDepth, r.width, openDepth);
        if (dir == Vector2Int.right) return new Rect(r.xMax, r.yMin, openDepth, r.height);
        return new Rect(r.xMin - openDepth, r.yMin, openDepth, r.height);
    }

    public bool CanOpen(int area, Vector2Int dir)
    {
        if (Growing || area < 0 || area >= areas.Count || OpenCount >= maxOpens) return false;
        var n = NewAreaRect(area, dir);
        if (Mathf.Max(Mathf.Abs(n.xMin), Mathf.Abs(n.xMax), Mathf.Abs(n.yMin), Mathf.Abs(n.yMax)) > maxReach) return false;
        foreach (var a in areas)
            if (Mathf.Min(a.xMax, n.xMax) - Mathf.Max(a.xMin, n.xMin) > 0.5f && Mathf.Min(a.yMax, n.yMax) - Mathf.Max(a.yMin, n.yMin) > 0.5f) return false; // 겹침
        return true;
    }

    /// <summary>area의 dir 변에 새 구역을 엶. duration 동안 늘어나며 열림.</summary>
    public bool OpenSide(int area, Vector2Int dir, float duration = 2f)
    {
        if (!CanOpen(area, dir)) return false;
        areas.Add(NewAreaRect(area, dir));
        growIndex = areas.Count - 1; growDir = dir;
        growT = duration <= 0f ? 1f : 0f; growTime = Mathf.Max(0.01f, duration);
        dirty = true;
        if (growT >= 1f) { Save(); AreaOpened?.Invoke(areas[growIndex]); }
        return true;
    }

    /// <summary>저장된 열린 구역 지우기 (중앙만 남김).</summary>
    [ContextMenu("열린 구역 초기화")]
    public void ResetAreas()
    {
        areas.Clear(); areas.Add(BaseRect); growT = 1f; growIndex = -1; dirty = true;
        if (Application.isPlaying) PlayerPrefs.DeleteKey(SaveKey);
    }

    void Load()
    {
        loaded = true;
        areas.Clear(); areas.Add(BaseRect);
        if (!persist || !Application.isPlaying) return;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var st = System.Globalization.NumberStyles.Float;
        foreach (var part in PlayerPrefs.GetString(SaveKey, "").Split(';'))
        {
            var v = part.Split(',');
            if (v.Length == 4 && float.TryParse(v[0], st, inv, out float x) && float.TryParse(v[1], st, inv, out float y)
                && float.TryParse(v[2], st, inv, out float w) && float.TryParse(v[3], st, inv, out float h))
                areas.Add(new Rect(x, y, w, h));
        }
    }

    void Save()
    {
        if (!persist || !Application.isPlaying) return;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i < areas.Count; i++)
        {
            var a = areas[i];
            if (sb.Length > 0) sb.Append(';');
            sb.Append(a.x.ToString(inv)).Append(',').Append(a.y.ToString(inv)).Append(',').Append(a.width.ToString(inv)).Append(',').Append(a.height.ToString(inv));
        }
        PlayerPrefs.SetString(SaveKey, sb.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>현재 구역 사각형 (열리는 구역은 늘어난 만큼).</summary>
    Rect LiveRect(int i)
    {
        var r = areas[i];
        if (i != growIndex || growT >= 1f) return r;
        float k = growT * growT * (3f - 2f * growT);
        // 붙은 변(원래 구역 쪽)은 고정, 반대쪽이 늘어남
        if (growDir == Vector2Int.right) r.width *= k;
        else if (growDir == Vector2Int.left) r.xMin = r.xMax - r.width * k;
        else if (growDir == Vector2Int.up) r.height *= k;
        else r.yMin = r.yMax - r.height * k;
        return r;
    }

    /// <summary>벽 메시/콜라이더와 이동 경계(Arena)를 현재 구역들에 맞춤.</summary>
    public void Apply()
    {
        if (!loaded) Load();
        dirty = false;
        rectBuf.Clear(); edgeBuf.Clear();
        for (int i = 0; i < areas.Count; i++) rectBuf.Add(LiveRect(i));
        // 바깥 변 = 각 구역의 변에서 맞닿은 다른 구역이 덮는 구간을 뺀 나머지
        for (int i = 0; i < rectBuf.Count; i++)
        {
            var r = rectBuf[i];
            AddOuter(i, r.yMax, r.xMin, r.xMax, true, true);   // 위 (+z)
            AddOuter(i, r.yMin, r.xMin, r.xMax, true, false);  // 아래 (-z)
            AddOuter(i, r.xMax, r.yMin, r.yMax, false, true);  // 오른쪽 (+x)
            AddOuter(i, r.xMin, r.yMin, r.yMax, false, false); // 왼쪽 (-x)
        }
        BuildWalls();
        if (Application.isPlaying) Arena.SetCells(rectBuf, edgeBuf);
    }

    /// <summary>
    /// 변 하나(horizontal: z=line, x∈[a,b] / 아니면 x=line, z∈[a,b])에서 반대편에 붙은 구역이 덮는 구간을 빼고 남은 선분을 바깥 경계로 추가.
    /// positive: 변이 +방향(위/오른쪽)을 향하는지. 선분은 바깥이 진행 방향의 오른쪽이 되도록 (시계 방향) 저장.
    /// </summary>
    void AddOuter(int self, float line, float a, float b, bool horizontal, bool positive)
    {
        spans.Clear(); spans.Add(new Vector2(a, b));
        for (int j = 0; j < rectBuf.Count; j++)
        {
            if (j == self) continue;
            var o = rectBuf[j];
            float touch = horizontal ? (positive ? o.yMin : o.yMax) : (positive ? o.xMin : o.xMax);
            if (Mathf.Abs(touch - line) > 0.01f) continue;
            float c0 = horizontal ? o.xMin : o.yMin, c1 = horizontal ? o.xMax : o.yMax;
            spanTmp.Clear();
            foreach (var s in spans)
            {
                if (c1 <= s.x || c0 >= s.y) { spanTmp.Add(s); continue; }
                if (c0 > s.x) spanTmp.Add(new Vector2(s.x, c0));
                if (c1 < s.y) spanTmp.Add(new Vector2(c1, s.y));
            }
            spans.Clear(); spans.AddRange(spanTmp);
        }
        foreach (var s in spans)
        {
            if (s.y - s.x < 0.05f) continue;
            if (horizontal) edgeBuf.Add(positive ? new Vector4(s.x, line, s.y, line) : new Vector4(s.y, line, s.x, line));
            else edgeBuf.Add(positive ? new Vector4(line, s.y, line, s.x) : new Vector4(line, s.x, line, s.y));
        }
    }

    void BuildWalls()
    {
        int layer = LayerMask.NameToLayer("Ignore Raycast");
        // 예전 버전(North/South/East/West 고정 벽) 정리
        foreach (var n in new[] { "North", "South", "East", "West" }) { var old = transform.Find(n); if (old) DestroyObj(old.gameObject); }
        // 스크립트 재컴파일 후에는 목록이 비므로 남아 있는 벽을 다시 모음
        if (walls.Count == 0) foreach (Transform ch in transform) if (ch.name == "Wall" && ch.GetComponent<MeshFilter>()) walls.Add(ch);
        while (walls.Count < edgeBuf.Count)
        {
            var w = new GameObject("Wall", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider)) { hideFlags = HideFlags.DontSave }.transform;
            w.SetParent(transform, false);
            if (layer >= 0) w.gameObject.layer = layer;
            var mr = w.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            walls.Add(w);
        }
        for (int i = 0; i < walls.Count; i++)
        {
            var w = walls[i];
            bool on = i < edgeBuf.Count;
            if (w.gameObject.activeSelf != on) w.gameObject.SetActive(on);
            if (!on) continue;
            var e = edgeBuf[i];
            var a = new Vector3(e.x, 0f, e.y); var b = new Vector3(e.z, 0f, e.w);
            float len = Vector3.Distance(a, b);
            Vector3 along = len > 0.001f ? (b - a) / len : Vector3.right;
            Vector3 outward = Vector3.Cross(along, Vector3.up); // 시계 방향 외곽선 → 바깥쪽
            w.localPosition = (a + b) * 0.5f;
            w.localRotation = Quaternion.LookRotation(outward);
            w.GetComponent<MeshRenderer>().sharedMaterial = material;
            var mf = w.GetComponent<MeshFilter>();
            if (!mf.sharedMesh) mf.sharedMesh = new Mesh { name = "BorderWall", hideFlags = HideFlags.DontSave };
            BuildMesh(mf.sharedMesh, len, height, Vector3.Dot(a, along)); // uv.x는 월드 기준이라 벽이 나뉘어도 무늬가 이어짐
            var box = w.GetComponent<BoxCollider>();
            box.center = new Vector3(0f, height * 0.5f, colliderThickness * 0.5f);
            box.size = new Vector3(len + colliderThickness * 2f, height + 4f, colliderThickness);
        }
    }

    /// <summary>벽 한 면: 로컬 x = 길이 방향(along), uv.x = 월드 기준 거리, uv.y = 높이 비율.</summary>
    static void BuildMesh(Mesh m, float len, float h, float u0)
    {
        float x = len * 0.5f;
        m.Clear();
        m.vertices = new[] { new Vector3(-x, 0f, 0f), new Vector3(x, 0f, 0f), new Vector3(-x, h, 0f), new Vector3(x, h, 0f) };
        m.uv = new[] { new Vector2(u0, 0f), new Vector2(u0 + len, 0f), new Vector2(u0, 1f), new Vector2(u0 + len, 1f) };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateNormals();
        m.RecalculateBounds();
    }

    static void DestroyObj(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

    void OnDestroy()
    {
        foreach (var w in walls)
            if (w) { var mf = w.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) DestroyObj(mf.sharedMesh); }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        foreach (var r in areas)
            Gizmos.DrawWireCube(new Vector3(r.center.x, height * 0.5f, r.center.y), new Vector3(r.width, height, r.height));
    }
}
