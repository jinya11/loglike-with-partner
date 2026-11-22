using UnityEngine;

/// <summary>졸개: Meshy 리그(mixamo 뼈) 그대로 사용. T자 팔을 내리고 두 발 걷기 + 망토 펄럭임.</summary>
public class MinionRig : EnemyRig
{
    public override string Prefab => "Enemy";
    public override string Model => "졸개";
    public override float WalkLen => 0.5f;
    public override float IdleLen => 2.2f;

    const float ArmDown = 72f, Thigh = 30f, Knee = 45f, ArmSwing = 24f, Bob = 0.035f;
    // 이 모델은 mixamo "Right"가 +X 쪽
    const string P = "mixamorig:";
    const string LegA = P + "RightUpLeg", KneeA = P + "RightLeg", FootA = P + "RightFoot";
    const string LegB = P + "LeftUpLeg", KneeB = P + "LeftLeg", FootB = P + "LeftFoot";
    const string ArmA = P + "RightArm", ForeA = P + "RightForeArm", ArmB = P + "LeftArm", ForeB = P + "LeftForeArm";
    const string Hips = P + "Hips", Spine = P + "Spine1", Chest = P + "Spine2", Head = P + "Head";
    const string Cape = "Bone_024", CapeA = "Bone_041", CapeB = "Bone_046";

    public override void Pose(Motion m, float t, Bounds b, PoseWriter p)
    {
        float ph = t * Tau, s = Mathf.Sin(ph), c = Mathf.Cos(ph), H = b.size.y;
        if (m == Motion.Walk)
        {
            float ta = Thigh * s, tb = -Thigh * s;
            float ka = Mathf.Max(0f, -c) * Knee, kb = Mathf.Max(0f, c) * Knee;
            p.Rot(LegA, ta, 0f, 0f); p.Rot(LegB, tb, 0f, 0f);
            p.Rot(KneeA, ka, 0f, 0f); p.Rot(KneeB, kb, 0f, 0f);
            p.Rot(FootA, -(ta + ka) * 0.6f, 0f, 0f); p.Rot(FootB, -(tb + kb) * 0.6f, 0f, 0f);
            p.Rot(ArmA, -ArmSwing * s, 0f, -ArmDown); p.Rot(ArmB, ArmSwing * s, 0f, ArmDown);
            p.Rot(ForeA, -15f, -10f, 0f); p.Rot(ForeB, -15f, 10f, 0f);
            p.Rot(Hips, 0f, 6f * s, 3f * Mathf.Cos(ph * 2f));
            p.Rot(Spine, 5f, -3f * s, 0f); p.Rot(Chest, 3f, -4f * s, 0f);
            p.Rot(Head, -4f + 2f * Mathf.Cos(ph * 2f), 4f * s, 0f);
            float flap = 14f + 6f * Mathf.Sin(ph * 2f);
            p.Rot(Cape, flap, 0f, 0f); p.Rot(CapeA, flap * 0.6f, 0f, 0f); p.Rot(CapeB, flap * 0.6f, 0f, 0f);
            p.Move(Hips, 0f, Mathf.Abs(c) * Bob * H - Bob * H * 0.5f, 0f);
        }
        else
        {
            p.Rot(LegA, 0f, 0f, 0f); p.Rot(LegB, 0f, 0f, 0f); p.Rot(KneeA, 0f, 0f, 0f); p.Rot(KneeB, 0f, 0f, 0f);
            p.Rot(FootA, 0f, 0f, 0f); p.Rot(FootB, 0f, 0f, 0f);
            p.Rot(ArmA, 0f, 0f, -ArmDown + 2f * s); p.Rot(ArmB, 0f, 0f, ArmDown - 2f * s);
            p.Rot(ForeA, -10f, -12f, 0f); p.Rot(ForeB, -10f, 12f, 0f);
            p.Rot(Hips, 0f, 0f, 0f);
            p.Rot(Spine, -1.5f * s, 0f, 0f); p.Rot(Chest, -1f * s, 0f, 0f);
            p.Rot(Head, 1.5f * s, 6f * Mathf.Sin(ph * 0.5f), 0f);
            float flap = 3f + 2f * s;
            p.Rot(Cape, flap, 0f, 0f); p.Rot(CapeA, flap, 0f, 0f); p.Rot(CapeB, flap, 0f, 0f);
            p.Move(Hips, 0f, -0.006f * H * (0.5f + 0.5f * s), 0f);
        }
    }
}
