using System.Collections;
using UnityEngine;

/// <summary>거신: 플레이어 근처의 적에게 예고 후 낙뢰.</summary>
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
            // 플레이어 주변에서 가장 가까운 적 우선, 없으면 임의의 적
            var target = stage.NearestEnemy(PlayerController.I.Position, 12f);
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
}
