using UnityEngine;

/// <summary>점액괴: 바닥/몸/정수리 3뼈. 평소 출렁이며 숨쉬기, 쫓아올 때 납작-쭉 늘어나며 통통 뛰어옴.</summary>
public class SlimeRig : EnemyRig
{
    public override string Prefab => "Enemy_Slime";
    public override string Model => "점액괴";
    public override float WalkLen => 0.6f;

    const int Base = 0, Body = 1, Top = 2;
    static readonly string[] N = { "Base", "Body", "Top" };
    const float Hop = 0.3f, Squash = 0.2f, Stretch = 0.14f;

    public override BoneSpec[] Skeleton(Vector3[] v, Bounds b) => new[]
    {
        new BoneSpec(N[Base], -1, new Vector3(0f, 0f, b.center.z)),
        new BoneSpec(N[Body], Base, new Vector3(0f, b.size.y * 0.4f, b.center.z)),
        new BoneSpec(N[Top], Body, new Vector3(0f, b.size.y * 0.75f, b.center.z)),
    };

    public override void Weigh(Vector3 q, Bounds b, float[] w)
    {
        float H = b.size.y;
        float body = S(H * 0.08f, H * 0.35f, q.y), top = S(H * 0.55f, H * 0.85f, q.y);
        w[Base] = 1f - body; w[Body] = body - top; w[Top] = top;
    }

    public override void Pose(Motion m, float t, Bounds b, PoseWriter p)
    {
        float ph = t * Tau, s = Mathf.Sin(ph), H = b.size.y;
        if (m == Motion.Walk)
        {
            float air = Mathf.Max(0f, s), ground = Mathf.Max(0f, -s);
            float sy = 1f - Squash * ground + Stretch * air, sxz = 1f + Squash * 0.6f * ground - Stretch * 0.5f * air;
            p.Scale(N[Base], sxz, sy, sxz);
            p.Move(N[Base], 0f, Hop * H * air, 0f);
            p.Rot(N[Base], 6f * air, 0f, 0f);
            p.Rot(N[Top], -8f * Mathf.Cos(ph), 0f, 4f * Mathf.Sin(ph * 2f));
            p.Scale(N[Body], 1f, 1f, 1f);
        }
        else
        {
            p.Scale(N[Base], 1f, 1f, 1f);
            p.Move(N[Base], 0f, 0f, 0f);
            p.Rot(N[Base], 0f, 0f, 0f);
            p.Rot(N[Top], 3f * Mathf.Sin(ph + 1f), 0f, 3f * Mathf.Sin(ph * 2f));
            p.Scale(N[Body], 1f + 0.04f * s, 1f - 0.06f * s, 1f + 0.04f * s);
        }
    }
}
