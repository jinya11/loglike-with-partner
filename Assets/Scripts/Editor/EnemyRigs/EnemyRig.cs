using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 한 종류의 리그/모션 정의 (적마다 서브클래스 하나, 그 적의 수치만 가짐).
/// 좌표계: 모델 기준, 발바닥 y=0, 앞 = +Z. 회전은 "쉬는 자세 기준 월드 축" 오일러(도), 이동은 월드 오프셋, 크기는 배율.
/// EnemyRigBuilder가 이 정의로 뼈/가중치/클립/Animator 컨트롤러/모델 프리팹을 만들고 적 프리팹에 끼움.
/// </summary>
public abstract class EnemyRig
{
    public static readonly EnemyRig[] All = { new MinionRig(), new ChargerRig(), new LeechRig(), new SlimeRig(), new ScreamerRig() };

    public enum Motion { Idle, Walk, Special }

    public struct BoneSpec
    {
        public string name; public int parent; public Vector3 pos;
        public BoneSpec(string n, int p, Vector3 v) { name = n; parent = p; pos = v; }
    }

    /// <summary>적 프리팹 이름 (Assets/Prototype/Prefabs/{Prefab}.prefab).</summary>
    public abstract string Prefab { get; }
    /// <summary>모델 이름 (ENTITY/ENEMY/{Model}.fbx, 재질 {Model}.mat).</summary>
    public abstract string Model { get; }
    /// <summary>이 이동 속도에서 걷기 모션 1배속.</summary>
    public virtual float WalkRef => 2.6f;
    public virtual float IdleLen => 1.6f;
    public virtual float WalkLen => 0.55f;
    public virtual float SpecialLen => 0.5f;
    /// <summary>Special = 돌진 중 반복 모션 (Charge 파라미터).</summary>
    public virtual bool HasCharge => false;
    /// <summary>Special = 능력 사용 1회 모션 (Ability 트리거).</summary>
    public virtual bool HasAbility => false;

    /// <summary>뼈 자동 배치 (null = 모델에 이미 뼈가 있음). v = 정규화된 정점.</summary>
    public virtual BoneSpec[] Skeleton(Vector3[] v, Bounds b) => null;
    /// <summary>정점 하나의 뼈별 가중치 (합은 아무거나, 나중에 정규화).</summary>
    public virtual void Weigh(Vector3 q, Bounds b, float[] w) { }
    /// <summary>몸에서 떨어진 작은 덩어리(구슬 등)를 통째로 붙일 뼈 (-1 = 없음).</summary>
    public virtual int IslandBone => -1;

    /// <summary>모션의 한 시점 자세. t = 0~1 (한 주기).</summary>
    public abstract void Pose(Motion m, float t, Bounds b, PoseWriter p);

    protected const float Tau = Mathf.PI * 2f;
    protected static float S(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
}

/// <summary>모션 한 시점의 뼈별 회전/이동/크기 기록.</summary>
public class PoseWriter
{
    public readonly Dictionary<string, Vector3> rot = new(), move = new(), scale = new();
    public void Clear() { rot.Clear(); move.Clear(); scale.Clear(); }
    public void Rot(string bone, float x, float y, float z) => rot[bone] = new Vector3(x, y, z);
    public void Move(string bone, float x, float y, float z) => move[bone] = new Vector3(x, y, z);
    public void Scale(string bone, float x, float y, float z) => scale[bone] = new Vector3(x, y, z);
}
