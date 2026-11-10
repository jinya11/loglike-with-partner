using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아레나용 격자 길찾기 (A*, 8방향). 원형 또는 정사각형(지역 스테이지 보더) 경계.
/// - 벽/장애물 칸은 막힘 (스테이지 시작 시 물리 검사로 한 번 생성)
/// - 적 주변은 위험 비용, 용암 같은 피해 구역도 비용 → "적을 피하면서 가장 빠른 길"
/// 할당 없는 고정 버퍼 사용.
/// </summary>
public sealed class GridNav
{
    readonly float cell;
    readonly int size;
    readonly Vector3 origin;
    readonly bool[] blocked;
    readonly float[] staticCost, danger, g, f;
    readonly int[] parent, heap, heapPos, visit;
    int visitId, heapCount;

    static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    static readonly int[] DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };
    static readonly float[] STEP = { 1f, 1f, 1f, 1f, 1.4142f, 1.4142f, 1.4142f, 1.4142f };

    /// <summary>
    /// 원형 경계(반지름 radius, 중심 원점)용. square면 radius 대신 현재 Arena 칸 경계(Arena.Bounds)를 덮는 격자를 만들고
    /// 경계 밖 칸은 막힘 (보더가 열리면 StageDirector가 격자를 다시 만듦).
    /// </summary>
    public GridNav(float radius, float cellSize, float clearance, int obstacleMask, bool square = false)
    {
        cell = cellSize;
        float half = radius;
        Vector3 center = Vector3.zero;
        if (square) { var b = Arena.Bounds; half = Mathf.Max(b.width, b.height) * 0.5f + 1f; center = new Vector3(b.center.x, 0f, b.center.y); }
        size = Mathf.CeilToInt(half * 2f / cell) + 1;
        origin = center - new Vector3(half, 0f, half);
        int count = size * size;
        blocked = new bool[count]; staticCost = new float[count]; danger = new float[count];
        g = new float[count]; f = new float[count];
        parent = new int[count]; heap = new int[count]; heapPos = new int[count]; visit = new int[count];

        Physics.SyncTransforms();
        float inner = radius - clearance - 0.3f;
        for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
            {
                int i = z * size + x;
                Vector3 c = Center(i); c.y = Ground.HeightAt(c); // 고저차: 지면 높이 기준으로 장애물 검사
                blocked[i] = (square ? !Arena.Contains(c, clearance + 0.3f) : c.x * c.x + c.z * c.z > inner * inner)
                    || Physics.CheckCapsule(c + Vector3.up * 0.3f, c + Vector3.up * 1.2f, clearance, obstacleMask, QueryTriggerInteraction.Ignore)
                    || Ground.HasCliff(c.x, c.z, cell * 0.5f + clearance * 0.75f); // 고저차: 오를 수 없는 절벽은 벽 (경사로는 통과)
            }

        // 바닥 구역 비용 (용암은 크게, 눈밭/물은 약간)
        foreach (var zone in StageZone.All)
            for (int i = 0; i < count; i++)
                if (!blocked[i] && zone.Contains(Center(i)))
                    staticCost[i] += zone.type == ZoneType.Damage ? 25f : zone.type == ZoneType.Heal ? 0f : 1.5f;

        // 경사 비용: 언덕을 오르내리는 길은 약간 돌아가도 평지를 선호 (완전히 막지는 않음)
        if (Ground.HasHills)
            for (int i = 0; i < count; i++)
                if (!blocked[i]) { Vector3 c = Center(i); staticCost[i] += Ground.Slope(c.x, c.z) * 1.5f; }
    }

    bool Free(int i) => !blocked[i];

    public Vector3 Center(int i) => origin + new Vector3((i % size + 0.5f) * cell, 0f, (i / size + 0.5f) * cell);

    int Index(Vector3 p)
    {
        int x = Mathf.Clamp((int)((p.x - origin.x) / cell), 0, size - 1);
        int z = Mathf.Clamp((int)((p.z - origin.z) / cell), 0, size - 1);
        return z * size + x;
    }

    /// <summary>적 위치 기반 위험 비용 갱신 (가까울수록 큼).</summary>
    public void UpdateDanger(List<Enemy> enemies, float radius, float weight)
    {
        System.Array.Clear(danger, 0, danger.Length);
        int rc = Mathf.CeilToInt(radius / cell);
        float r2 = radius * radius;
        for (int k = 0; k < enemies.Count; k++)
        {
            Vector3 e = enemies[k].transform.position;
            int ci = Index(e), cx = ci % size, cz = ci / size;
            for (int z = Mathf.Max(0, cz - rc); z <= Mathf.Min(size - 1, cz + rc); z++)
                for (int x = Mathf.Max(0, cx - rc); x <= Mathf.Min(size - 1, cx + rc); x++)
                {
                    int i = z * size + x;
                    Vector3 c = Center(i);
                    float dx = c.x - e.x, dz = c.z - e.z, d2 = dx * dx + dz * dz;
                    if (d2 < r2) danger[i] += weight * (1f - d2 / r2);
                }
        }
    }

    /// <summary>막히지 않은 가장 가까운 칸 중심.</summary>
    public Vector3 NearestFree(Vector3 p)
    {
        int s = Index(p);
        if (Free(s)) return p;
        int sx = s % size, sz = s / size;
        for (int r = 1; r < size; r++)
            for (int z = sz - r; z <= sz + r; z++)
                for (int x = sx - r; x <= sx + r; x++)
                {
                    if (x < 0 || z < 0 || x >= size || z >= size) continue;
                    if (Mathf.Abs(x - sx) != r && Mathf.Abs(z - sz) != r) continue;
                    int i = z * size + x;
                    if (Free(i)) return Center(i);
                }
        return p;
    }

    /// <summary>A* 경로. 성공하면 path에 월드 좌표 경유점(목표 포함)을 채움.</summary>
    public bool FindPath(Vector3 from, Vector3 to, List<Vector3> path)
    {
        path.Clear();
        int start = Index(from), goal = Index(NearestFree(to));
        if (start == goal) { path.Add(to); return true; }
        visitId++;
        heapCount = 0;
        Open(start, 0f, -1, goal);
        while (heapCount > 0)
        {
            int cur = Pop();
            if (cur == goal) { Build(cur, to, path); return true; }
            int cx = cur % size, cz = cur / size;
            for (int d = 0; d < 8; d++)
            {
                int nx = cx + DX[d], nz = cz + DZ[d];
                if (nx < 0 || nz < 0 || nx >= size || nz >= size) continue;
                int n = nz * size + nx;
                if (!Free(n)) continue;
                if (d >= 4 && (!Free(cz * size + nx) || !Free(nz * size + cx))) continue; // 모서리 끼임 방지
                float ng = g[cur] + STEP[d] * (1f + staticCost[n] + danger[n]);
                if (visit[n] == visitId && ng >= g[n]) continue;
                Open(n, ng, cur, goal);
            }
        }
        return false;
    }

    void Open(int i, float gi, int from, int goal)
    {
        float h = Vector2.Distance(new Vector2(i % size, i / size), new Vector2(goal % size, goal / size));
        bool fresh = visit[i] != visitId;
        visit[i] = visitId;
        g[i] = gi; f[i] = gi + h; parent[i] = from;
        if (fresh) { heap[heapCount] = i; heapPos[i] = heapCount; heapCount++; Up(heapPos[i]); }
        else if (heapPos[i] >= 0) Up(heapPos[i]);
        else { heap[heapCount] = i; heapPos[i] = heapCount; heapCount++; Up(heapPos[i]); }
    }

    void Build(int cur, Vector3 to, List<Vector3> path)
    {
        while (parent[cur] >= 0) { path.Add(Center(cur)); cur = parent[cur]; }
        path.Reverse();
        if (path.Count > 0) path[path.Count - 1] = NearestFree(to);
    }

    int Pop()
    {
        int top = heap[0];
        heapPos[top] = -1;
        heapCount--;
        if (heapCount > 0) { heap[0] = heap[heapCount]; heapPos[heap[0]] = 0; Down(0); }
        return top;
    }

    void Up(int k)
    {
        while (k > 0)
        {
            int p = (k - 1) >> 1;
            if (f[heap[p]] <= f[heap[k]]) break;
            Swap(k, p); k = p;
        }
    }

    void Down(int k)
    {
        while (true)
        {
            int l = k * 2 + 1, r = l + 1, m = k;
            if (l < heapCount && f[heap[l]] < f[heap[m]]) m = l;
            if (r < heapCount && f[heap[r]] < f[heap[m]]) m = r;
            if (m == k) return;
            Swap(k, m); k = m;
        }
    }

    void Swap(int a, int b)
    {
        int t = heap[a]; heap[a] = heap[b]; heap[b] = t;
        heapPos[heap[a]] = a; heapPos[heap[b]] = b;
    }

    // =====================================================================
    // 흐름장 (적 전체용): 목표 칸에서 거꾸로 Dijkstra → 각 칸이 목표 쪽 다음 칸을 가리킴.
    // 고저차 스테이지에서 적이 절벽을 돌아 경사로로 오르게 함 (수백 마리가 A* 없이 공유).
    // =====================================================================
    float[] flowDist;
    int[] flowNext, flowHeap, flowPos;
    int flowCount, flowGoal = -1;
    public bool HasFlow => flowGoal >= 0;

    public bool IsFree(Vector3 p) => Free(Index(p));

    /// <summary>목표 위치로 가는 흐름장 다시 계산 (StageDirector가 주기적으로).</summary>
    public void BuildFlow(Vector3 target)
    {
        int n = size * size;
        if (flowDist == null) { flowDist = new float[n]; flowNext = new int[n]; flowHeap = new int[n]; flowPos = new int[n]; }
        for (int i = 0; i < n; i++) { flowDist[i] = float.MaxValue; flowNext[i] = -1; flowPos[i] = -2; }
        int goal = Index(NearestFree(target));
        flowGoal = goal;
        flowCount = 0;
        flowDist[goal] = 0f;
        FlowPush(goal);
        while (flowCount > 0)
        {
            int cur = FlowPop();
            int cx = cur % size, cz = cur / size;
            for (int d = 0; d < 8; d++)
            {
                int nx = cx + DX[d], nz = cz + DZ[d];
                if (nx < 0 || nz < 0 || nx >= size || nz >= size) continue;
                int nb = nz * size + nx;
                if (!Free(nb)) continue;
                if (d >= 4 && (!Free(cz * size + nx) || !Free(nz * size + cx))) continue;
                float nd = flowDist[cur] + STEP[d] * (1f + staticCost[nb] * 0.3f);
                if (nd >= flowDist[nb]) continue;
                flowDist[nb] = nd;
                flowNext[nb] = cur;
                if (flowPos[nb] >= 0) FlowUp(flowPos[nb]); else FlowPush(nb);
            }
        }
    }

    /// <summary>
    /// 흐름장을 따라 목표로 가는 방향 (수평, 정규화). 막힌 칸이면 가장 가까운 열린 칸 쪽. 쓸 수 없으면 false.
    /// </summary>
    public bool FlowDir(Vector3 p, out Vector3 dir)
    {
        dir = Vector3.zero;
        if (flowGoal < 0) return false;
        int i = Index(p);
        if (!Free(i)) { Vector3 q = NearestFree(p) - p; q.y = 0f; if (q.sqrMagnitude < 0.0001f) return false; dir = q.normalized; return true; }
        int nx = flowNext[i];
        if (nx < 0) return false;
        // 두 칸 앞을 보면 지그재그가 줄어듦
        int nn = flowNext[nx];
        Vector3 t = Center(nn >= 0 && IsLine(i, nn) ? nn : nx) - p; t.y = 0f;
        if (t.sqrMagnitude < 0.0001f) return false;
        dir = t.normalized;
        return true;
    }

    /// <summary>두 칸 사이 직선이 막힌 칸을 지나지 않는지 (간단히 중간 칸만 검사).</summary>
    bool IsLine(int a, int b)
    {
        int ax = a % size, az = a / size, bx = b % size, bz = b / size;
        return Free(((az + bz) / 2) * size + (ax + bx) / 2) && Free(az * size + bx) && Free(bz * size + ax);
    }

    void FlowPush(int i) { flowHeap[flowCount] = i; flowPos[i] = flowCount; flowCount++; FlowUp(flowCount - 1); }

    int FlowPop()
    {
        int top = flowHeap[0];
        flowPos[top] = -1;
        flowCount--;
        if (flowCount > 0) { flowHeap[0] = flowHeap[flowCount]; flowPos[flowHeap[0]] = 0; FlowDown(0); }
        return top;
    }

    void FlowUp(int k)
    {
        while (k > 0)
        {
            int p = (k - 1) >> 1;
            if (flowDist[flowHeap[p]] <= flowDist[flowHeap[k]]) break;
            FlowSwap(k, p); k = p;
        }
    }

    void FlowDown(int k)
    {
        while (true)
        {
            int l = k * 2 + 1, r = l + 1, m = k;
            if (l < flowCount && flowDist[flowHeap[l]] < flowDist[flowHeap[m]]) m = l;
            if (r < flowCount && flowDist[flowHeap[r]] < flowDist[flowHeap[m]]) m = r;
            if (m == k) return;
            FlowSwap(k, m); k = m;
        }
    }

    void FlowSwap(int a, int b)
    {
        int t = flowHeap[a]; flowHeap[a] = flowHeap[b]; flowHeap[b] = t;
        flowPos[flowHeap[a]] = a; flowPos[flowHeap[b]] = b;
    }
}
