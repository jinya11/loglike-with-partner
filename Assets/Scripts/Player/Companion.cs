using System.Collections;
using UnityEngine;

/// <summary>거신: 작은 생명체에 가장 가까운 적에게 예고 후 낙뢰.</summary>
public class Companion : CompanionBase
{
    [SerializeField] float telegraphTime = 0.6f;

    static readonly Color Telegraph = new Color(1f, 0.3f, 0.2f), Strike = new Color(1f, 0.95f, 0.6f);
    WaitForSeconds waitTelegraph, waitGap;

    protected override void Start()
    {
        base.Start();
        waitTelegraph = new WaitForSeconds(telegraphTime);
        waitGap = new WaitForSeconds(0.15f);
    }

    protected override IEnumerator Act(PlayerStats s)
    {
        var stage = StageDirector.I;
        for (int i = 0; i < s.companionStrikes; i++)
        {
            // 생명체에 가장 가까운 적 우선 (디펜스), 없으면 임의의 적
            Vector3 guard = Ward.I != null ? Ward.I.Position : PlayerController.I.Position;
            var target = stage.NearestEnemy(guard, 12f);
            if (target == null && Enemy.Active.Count > 0) target = Enemy.Active[Random.Range(0, Enemy.Active.Count)];
            if (target == null) yield break;

            Vector3 p = target.transform.position;
            float r = s.companionRadius * s.areaMul, d = r * 2f;
            stage.SpawnFx(p + Vector3.up * 0.03f, new Vector3(d, 0.02f, d), new Vector3(0.2f, 0.02f, 0.2f), telegraphTime, Telegraph);
            yield return waitTelegraph;
            stage.DamageArea(p, r, s.companionDamage);
            stage.SpawnFx(p + Vector3.up * 6f, new Vector3(r * 0.5f, 6f, r * 0.5f), new Vector3(0.05f, 6f, 0.05f), 0.2f, Strike);
            stage.SpawnFx(p + Vector3.up * 0.04f, new Vector3(d, 0.02f, d), new Vector3(d * 1.2f, 0.02f, d * 1.2f), 0.2f, Strike);
            yield return waitGap;
        }
    }

    /// <summary>폭격 (거신): 넓은 예고 후 범위 안에 낙뢰를 연속으로 쏟아붓고, 마지막에 중앙 대낙뢰.</summary>
    protected override IEnumerator Bombard(Vector3 target, PlayerStats s)
    {
        var stage = StageDirector.I;
        float R = bombardRadius * s.areaMul, D = R * 2f;
        stage.SpawnFx(target + Vector3.up * 0.03f, new Vector3(D, 0.02f, D), new Vector3(D * 0.9f, 0.02f, D * 0.9f), 1f, Telegraph);
        yield return new WaitForSeconds(1f);

        var gap = new WaitForSeconds(0.08f);
        float r = s.companionRadius, d = r * 2f;
        for (int i = 0; i < 12; i++)
        {
            Vector2 o = Random.insideUnitCircle * R;
            Vector3 p = target + new Vector3(o.x, 0f, o.y);
            stage.DamageArea(p, r, s.companionDamage * 1.5f);
            stage.SpawnFx(p + Vector3.up * 8f, new Vector3(r * 0.4f, 8f, r * 0.4f), new Vector3(0.05f, 8f, 0.05f), 0.18f, Strike);
            stage.SpawnFx(p + Vector3.up * 0.04f, new Vector3(d, 0.02f, d), new Vector3(d * 1.3f, 0.02f, d * 1.3f), 0.2f, Strike);
            yield return gap;
        }
        stage.DamageArea(target, R * 0.6f, s.companionDamage * 4f);
        stage.SpawnFx(target + Vector3.up * 12f, new Vector3(R * 0.5f, 12f, R * 0.5f), new Vector3(0.1f, 12f, 0.1f), 0.35f, Strike);
        stage.SpawnFx(target + Vector3.up * 0.05f, new Vector3(R, 0.02f, R), new Vector3(D * 1.1f, 0.02f, D * 1.1f), 0.35f, Strike);
    }
}