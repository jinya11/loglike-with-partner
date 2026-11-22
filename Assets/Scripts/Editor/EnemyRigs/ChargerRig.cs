using UnityEngine;

/// <summary>돌진꾼(코뿔소): 네 다리 뼈 자동 배치. 평소 대각선 걸음(트롯), 돌진 중 앞뒤 다리를 모아 뛰는 질주.</summary>
public class ChargerRig : EnemyRig
{
    public override string Prefab => "Enemy_Charger";
    public override string Model => "돌진꾼";
    public override bool HasCharge => true;
    public override float WalkLen => 0.5f;
    public override float SpecialLen => 0.34f;

    const int Body = 0, Head = 1, FL = 2, FR = 3, BL = 4, BR = 5;
    static readonly string[] N = { "Body", "Head", "Leg_FL", "Leg_FR", "Leg_BL", "Leg_BR" };
    const float LegTop = 0.32f, LegBlend = 0.12f, Trot = 26f, Gallop = 42f;

    public override BoneSpec[] Skeleton(Vector3[] v, Bounds b)
    {
        float H = b.size.y, L = b.size.z, cz = b.center.z;
        // 다리 위치 = 발 근처(아래 15%) 정점의 사분면 평균
        var sum = new Vector3[4]; var cnt = new int[4];
        foreach (var q in v)
        {
            if (q.y > H * 0.15f) continue;
            int i = (q.z >= cz ? 0 : 2) + (q.x >= 0f ? 0 : 1);
            sum[i] += q; cnt[i]++;
        }
        var bones = new BoneSpec[6];
        bones[Body] = new BoneSpec(N[Body], -1, new Vector3(0f, H * 0.5f, cz));
        bones[Head] = new BoneSpec(N[Head], Body, new Vector3(0f, H * 0.55f, cz + L * 0.22f));
        for (int i = 0; i < 4; i++)
        {
            Vector3 f = cnt[i] > 0 ? sum[i] / cnt[i] : new Vector3(i % 2 == 0 ? 0.3f : -0.3f, 0f, i < 2 ? L * 0.25f : -L * 0.25f);
            bones[FL + i] = new BoneSpec(N[FL + i], Body, new Vector3(f.x, H * LegTop, f.z));
        }
        return bones;
    }

    public override void Weigh(Vector3 q, Bounds b, float[] w)
    {
        float H = b.size.y, L = b.size.z, cz = b.center.z;
        float leg = 1f - S(H * LegTop, H * (LegTop + LegBlend), q.y);
        int i = (q.z >= cz ? 0 : 2) + (q.x >= 0f ? 0 : 1);
        w[FL + i] = leg;
        float head = S(cz + L * 0.12f, cz + L * 0.28f, q.z) * (1f - leg);
        w[Head] = head;
        w[Body] = Mathf.Max(0f, 1f - leg - head);
    }

    public override void Pose(Motion m, float t, Bounds b, PoseWriter p)
    {
        float ph = t * Tau, s = Mathf.Sin(ph), H = b.size.y;
        switch (m)
        {
            case Motion.Walk: // 대각선 다리 쌍이 함께
                p.Rot(N[FL], Trot * s, 0f, 0f); p.Rot(N[BR], Trot * s, 0f, 0f);
                p.Rot(N[FR], -Trot * s, 0f, 0f); p.Rot(N[BL], -Trot * s, 0f, 0f);
                p.Rot(N[Body], 0f, 0f, 3f * s);
                p.Rot(N[Head], 4f * Mathf.Sin(ph * 2f), 3f * s, 0f);
                p.Move(N[Body], 0f, Mathf.Abs(Mathf.Cos(ph)) * 0.04f * H, 0f);
                p.Scale(N[Body], 1f, 1f, 1f);
                break;
            case Motion.Special: // 질주: 앞다리 쌍 / 뒷다리 쌍, 몸 앞뒤로 출렁, 머리 숙여 뿔 내밀기
                float fr = Gallop * s, bk = Gallop * Mathf.Sin(ph + Mathf.PI * 0.7f);
                p.Rot(N[FL], fr, 0f, 0f); p.Rot(N[FR], fr * 0.85f, 0f, 0f);
                p.Rot(N[BL], bk, 0f, 0f); p.Rot(N[BR], bk * 0.85f, 0f, 0f);
                p.Rot(N[Body], 8f * Mathf.Sin(ph + 0.6f), 0f, 0f);
                p.Rot(N[Head], 16f, 0f, 0f);
                p.Move(N[Body], 0f, Mathf.Max(0f, Mathf.Sin(ph + 0.3f)) * 0.08f * H, 0f);
                p.Scale(N[Body], 1f, 1f, 1f + 0.05f * s);
                break;
            default: // 숨쉬기 + 두리번
                p.Rot(N[FL], 0f, 0f, 0f); p.Rot(N[FR], 0f, 0f, 0f); p.Rot(N[BL], 0f, 0f, 0f); p.Rot(N[BR], 0f, 0f, 0f);
                p.Rot(N[Body], 0f, 0f, 0f);
                p.Rot(N[Head], 3f * s, 10f * Mathf.Sin(ph * 0.5f), 0f);
                p.Move(N[Body], 0f, 0f, 0f);
                p.Scale(N[Body], 1f + 0.025f * s, 1f + 0.025f * s, 1f);
                break;
        }
    }
}
