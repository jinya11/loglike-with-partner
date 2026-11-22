using UnityEngine;

/// <summary>흡혈충(벌레): 양 날개 뼈 자동 배치. 늘 떠서 날갯짓, 쫓아올 때는 몸을 앞으로 숙이고 더 빠르게 펄럭임.</summary>
public class LeechRig : EnemyRig
{
    public override string Prefab => "Enemy_Leech";
    public override string Model => "흡혈충";
    public override float IdleLen => 0.6f;
    public override float WalkLen => 0.5f;

    const int Body = 0, WingL = 1, WingR = 2;
    static readonly string[] N = { "Body", "Wing_L", "Wing_R" };
    /// <summary>날개 판정: 이 높이(비율) 위 + 몸 가운데에서 이 폭(비율) 밖. 앞쪽(더듬이)은 제외.</summary>
    const float WingY = 0.5f, WingX = 0.07f, AntennaZ = 0.18f;
    const float Hover = 0.35f;

    static float WingMask(Vector3 q, Bounds b) =>
        S(b.size.y * WingY, b.size.y * (WingY + 0.08f), q.y)
        * S(b.size.x * WingX, b.size.x * (WingX + 0.06f), Mathf.Abs(q.x))
        * (1f - S(b.center.z + b.size.z * AntennaZ, b.center.z + b.size.z * (AntennaZ + 0.06f), q.z));

    public override BoneSpec[] Skeleton(Vector3[] v, Bounds b)
    {
        float wz = 0f; int c = 0;
        foreach (var q in v) if (WingMask(q, b) > 0.5f && q.y < b.size.y * (WingY + 0.15f)) { wz += q.z; c++; }
        wz = c > 0 ? wz / c : b.center.z;
        float hy = b.size.y * (WingY + 0.04f), hx = b.size.x * (WingX + 0.03f);
        return new[]
        {
            new BoneSpec(N[Body], -1, new Vector3(0f, b.size.y * 0.4f, b.center.z)),
            new BoneSpec(N[WingL], Body, new Vector3(hx, hy, wz)),
            new BoneSpec(N[WingR], Body, new Vector3(-hx, hy, wz)),
        };
    }

    public override void Weigh(Vector3 q, Bounds b, float[] w)
    {
        float wing = WingMask(q, b);
        w[q.x >= 0f ? WingL : WingR] = wing;
        w[Body] = 1f - wing;
    }

    public override void Pose(Motion m, float t, Bounds b, PoseWriter p)
    {
        float ph = t * Tau, H = b.size.y;
        bool chase = m == Motion.Walk;
        int flaps = chase ? 4 : 3;
        float amp = chase ? 38f : 30f;
        float f = Mathf.Sin(ph * flaps);
        p.Rot(N[WingL], 0f, 0f, 8f + amp * f);
        p.Rot(N[WingR], 0f, 0f, -8f - amp * f);
        float bob = Mathf.Sin(ph * (chase ? 2f : 1f)) * 0.05f * H;
        p.Move(N[Body], 0f, Hover + bob, 0f);
        p.Rot(N[Body], (chase ? 16f : 4f) + 3f * Mathf.Sin(ph * flaps + 1f), 0f, (chase ? 0f : 4f) * Mathf.Sin(ph));
    }
}
