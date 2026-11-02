using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 씬의 이동 경계. 원형(로비/구 스테이지) 또는 칸(정사각형 구역)들의 합집합(지역 스테이지, ArenaBorder가 설정).
/// 지역 스테이지는 처음엔 정사각형 한 칸이고, 보더를 열면 옆에 같은 크기의 칸이 붙어 경계가 그 모양을 따라감.
/// </summary>
public static class Arena
{
    public static float Radius = 30f;
    static readonly List<Rect> cells = new List<Rect>(16);   // XZ 평면 사각형 (x, z)
    static readonly List<Vector4> edges = new List<Vector4>(64); // 바깥 경계 선분 (x0, z0, x1, z1)
    static Rect bounds;

    /// <summary>칸 경계(지역 스테이지)인지. false면 원형.</summary>
    public static bool Square => cells.Count > 0;
    public static IReadOnlyList<Rect> Cells => cells;
    /// <summary>경계 전체를 감싸는 사각형 중심 (XZ).</summary>
    public static Vector3 Center => Square ? new Vector3(bounds.center.x, 0f, bounds.center.y) : Vector3.zero;
    /// <summary>중심에서 경계까지 가장 먼 거리 (원: 반지름, 칸: 감싸는 사각형의 긴 반 변).</summary>
    public static float Extent => Square ? Mathf.Max(bounds.width, bounds.height) * 0.5f : Radius;
    public static Rect Bounds => bounds;

    public static void SetCircle(float radius) { Radius = radius; cells.Clear(); edges.Clear(); }

    /// <summary>칸 목록과 바깥 경계 선분 설정 (ArenaBorder가 호출).</summary>
    public static void SetCells(IEnumerable<Rect> rects, IEnumerable<Vector4> outerEdges)
    {
        cells.Clear(); cells.AddRange(rects);
        edges.Clear(); edges.AddRange(outerEdges);
        if (cells.Count == 0) return;
        bounds = cells[0];
        foreach (var r in cells) bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, r.xMin), Mathf.Min(bounds.yMin, r.yMin), Mathf.Max(bounds.xMax, r.xMax), Mathf.Max(bounds.yMax, r.yMax));
        Radius = Extent;
    }

    /// <summary>XZ가 경계 안인지 (margin만큼 안쪽).</summary>
    public static bool Contains(Vector3 p, float margin = 0f)
    {
        if (!Square) { float r = Radius - margin; return p.x * p.x + p.z * p.z <= r * r; }
        foreach (var c in cells)
            if (p.x >= c.xMin && p.x <= c.xMax && p.z >= c.yMin && p.z <= c.yMax)
                return margin <= 0f || EdgeDistance(p) >= margin;
        return false;
    }

    /// <summary>XZ만 경계 안으로 제한 (y 유지).</summary>
    public static Vector3 Clamp(Vector3 p) => Clamp(p, 0.6f);

    public static Vector3 Clamp(Vector3 p, float margin)
    {
        if (Square)
        {
            if (Contains(p, margin)) return p;
            // 가장 가까운 칸 안쪽 점 (칸끼리 붙은 변은 안쪽이라 margin을 두지 않음)
            Vector3 best = p; float bestSq = float.MaxValue;
            foreach (var c in cells)
            {
                float x0 = c.xMin + (HasEdge(c.xMin, c.yMin, c.xMin, c.yMax) ? margin : 0f), x1 = c.xMax - (HasEdge(c.xMax, c.yMin, c.xMax, c.yMax) ? margin : 0f);
                float z0 = c.yMin + (HasEdge(c.xMin, c.yMin, c.xMax, c.yMin) ? margin : 0f), z1 = c.yMax - (HasEdge(c.xMin, c.yMax, c.xMax, c.yMax) ? margin : 0f);
                var q = new Vector3(Mathf.Clamp(p.x, x0, x1), p.y, Mathf.Clamp(p.z, z0, z1));
                float sq = (q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z);
                if (sq < bestSq) { bestSq = sq; best = q; }
            }
            return best;
        }
        float r = Radius - margin;
        float s = p.x * p.x + p.z * p.z;
        if (s <= r * r) return p;
        float k = r / Mathf.Sqrt(s);
        p.x *= k; p.z *= k;
        return p;
    }

    /// <summary>이 변 구간에 바깥 경계 선분이 겹치는지 (축 정렬 선분끼리).</summary>
    static bool HasEdge(float x0, float z0, float x1, float z1)
    {
        foreach (var e in edges)
        {
            bool vertical = Mathf.Approximately(x0, x1);
            if (vertical && Mathf.Approximately(e.x, e.z) && Mathf.Approximately(e.x, x0)
                && Mathf.Min(e.y, e.w) < Mathf.Max(z0, z1) - 0.01f && Mathf.Max(e.y, e.w) > Mathf.Min(z0, z1) + 0.01f) return true;
            if (!vertical && Mathf.Approximately(e.y, e.w) && Mathf.Approximately(e.y, z0)
                && Mathf.Min(e.x, e.z) < Mathf.Max(x0, x1) - 0.01f && Mathf.Max(e.x, e.z) > Mathf.Min(x0, x1) + 0.01f) return true;
        }
        return false;
    }

    /// <summary>가장 가까운 바깥 경계까지 거리 (원: 반지름 - 중심 거리).</summary>
    public static float EdgeDistance(Vector3 p)
    {
        if (!Square) return Radius - Mathf.Sqrt(p.x * p.x + p.z * p.z);
        float best = float.MaxValue;
        foreach (var e in edges)
        {
            var a = new Vector2(e.x, e.y); var b = new Vector2(e.z, e.w); var q = new Vector2(p.x, p.z);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
        }
        return best;
    }

    /// <summary>경계 안의 임의 지점 (frac = 칸/원 크기 대비 범위 비율).</summary>
    public static Vector3 RandomPoint(float frac)
    {
        if (Square)
        {
            // 넓이에 비례해 구역 선택
            float total = 0f;
            foreach (var a in cells) total += a.width * a.height;
            float pick = Random.value * total;
            var c = cells[cells.Count - 1];
            foreach (var a in cells) { pick -= a.width * a.height; if (pick <= 0f) { c = a; break; } }
            float hx = c.width * 0.5f * frac, hz = c.height * 0.5f * frac;
            return new Vector3(c.center.x + Random.Range(-hx, hx), 0f, c.center.y + Random.Range(-hz, hz));
        }
        Vector2 r = Random.insideUnitCircle * (Radius * frac);
        return new Vector3(r.x, 0f, r.y);
    }
}
