using UnityEngine;

/// <summary>비명귀(유령): 몸/꼬리/불꽃/주변 구슬 뼈. 떠다니며 꼬리 살랑, 구슬 공전. 공포 능력 때 부풀며 비명.</summary>
public class ScreamerRig : EnemyRig
{
    public override string Prefab => "Enemy_Screamer";
    public override string Model => "비명귀";
    public override bool HasAbility => true;
    public override float IdleLen => 2.4f;
    public override float WalkLen => 1.2f;
    public override float SpecialLen => 0.7f;

    const int Root = 0, Body = 1, Tail = 2, Flame = 3, Orbs = 4;
    static readonly string[] N = { "Root", "Body", "Tail", "Flame", "Orbs" };
    const float Hover = 0.12f;

    public override int IslandBone => Orbs;

    public override BoneSpec[] Skeleton(Vector3[] v, Bounds b)
    {
        float H = b.size.y, cz = b.center.z;
        return new[]
        {
            new BoneSpec(N[Root], -1, new Vector3(0f, 0f, cz)),
            new BoneSpec(N[Body], Root, new Vector3(0f, H * 0.5f, cz)),
            new BoneSpec(N[Tail], Body, new Vector3(0f, H * 0.32f, cz)),
            new BoneSpec(N[Flame], Body, new Vector3(0f, H * 0.8f, cz)),
            new BoneSpec(N[Orbs], Root, new Vector3(0f, H * 0.5f, cz)),
        };
    }

    public override void Weigh(Vector3 q, Bounds b, float[] w)
    {
        float H = b.size.y;
        float tail = 1f - S(H * 0.22f, H * 0.38f, q.y), flame = S(H * 0.74f, H * 0.86f, q.y);
        w[Tail] = tail; w[Flame] = flame; w[Body] = Mathf.Max(0f, 1f - tail - flame);
    }

    public override void Pose(Motion m, float t, Bounds b, PoseWriter p)
    {
        float ph = t * Tau, s = Mathf.Sin(ph), H = b.size.y;
        switch (m)
        {
            case Motion.Special: // 비명: 뒤로 젖히며 부풀었다 돌아옴, 구슬 빠르게 회전
                float k = Mathf.Sin(t * Mathf.PI);
                p.Move(N[Root], 0f, Hover + 0.05f * H * k, 0f);
                p.Rot(N[Body], -18f * k, 0f, 0f);
                p.Scale(N[Body], 1f + 0.3f * k, 1f + 0.35f * k, 1f + 0.3f * k);
                p.Rot(N[Tail], 0f, 0f, 25f * Mathf.Sin(ph * 3f) * k);
                p.Rot(N[Flame], -10f * k, 0f, 0f);
                p.Scale(N[Flame], 1f, 1f + 0.6f * k, 1f);
                p.Rot(N[Orbs], 0f, 720f * t, 0f);
                p.Scale(N[Orbs], 1f + 0.4f * k, 1f, 1f + 0.4f * k);
                break;
            default:
                bool chase = m == Motion.Walk;
                p.Move(N[Root], 0f, Hover + 0.05f * H * Mathf.Sin(ph * (chase ? 2f : 1f)), 0f);
                p.Rot(N[Body], chase ? 14f : 2f * s, 0f, chase ? 0f : 3f * Mathf.Sin(ph * 0.5f + 1f));
                p.Scale(N[Body], 1f, 1f + 0.03f * s, 1f);
                p.Rot(N[Tail], chase ? 10f : 0f, 0f, (chase ? 20f : 12f) * Mathf.Sin(ph * (chase ? 2f : 1f)));
                p.Rot(N[Flame], 0f, 0f, -8f * Mathf.Sin(ph * (chase ? 2f : 1f) + 1f));
                p.Scale(N[Flame], 1f, 1f, 1f);
                p.Rot(N[Orbs], 0f, 360f * t, 0f);
                p.Scale(N[Orbs], 1f, 1f, 1f);
                break;
        }
    }
}
