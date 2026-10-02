using System.Collections;
using UnityEngine;

/// <summary>풍신: 플레이어 주변 적이 몰린 쪽으로 돌풍을 일으켜 적들을 한쪽으로 멀리 밀어냄 (약한 피해).</summary>
public class WindCompanion : CompanionBase
{
    [SerializeField] float pushRadius = 9f, pushSpeed = 30f, damageMul = 0.3f, telegraphTime = 0.4f;

    static readonly Color Gust = new Color(0.6f, 1f, 0.9f), Warn = new Color(0.3f, 0.8f, 0.8f);
    WaitForSeconds waitTelegraph, waitGap;

    protected override void Start()
    {
        base.Start();
        waitTelegraph = new WaitForSeconds(telegraphTime);
        waitGap = new WaitForSeconds(0.3f);
    }

    protected override IEnumerator Act(PlayerStats s)
    {
        var stage = StageDirector.I;
        for (int i = 0; i < s.companionStrikes; i++)
        {
            Vector3 p = PlayerController.I.Position; p.y = 0f;
            float r = pushRadius * s.areaMul;

            // 적이 가장 많이 몰린 방향으로 밀어냄 (플레이어로부터 멀어지게)
            Vector3 sum = Vector3.zero;
            var list = Enemy.Active;
            for (int k = 0; k < list.Count; k++)
            {
                Vector3 d = list[k].transform.position - p; d.y = 0f;
                if (d.sqrMagnitude < r * r) sum += d;
            }
            if (sum.sqrMagnitude < 0.01f) yield break;
            Vector3 dir = sum.normalized;
            var rot = Quaternion.LookRotation(dir);

            // 예고: 밀어낼 방향으로 길쭉한 바람길
            Vector3 lane = p + dir * (r * 0.5f) + Vector3.up * 0.03f;
            stage.SpawnFx(lane, new Vector3(r * 1.2f, 0.02f, r * 0.2f), new Vector3(r * 1.2f, 0.02f, r * 1.6f), telegraphTime, Warn, rot);
            yield return waitTelegraph;

            stage.PushArea(p, r, dir * pushSpeed, s.companionDamage * damageMul);
            stage.SpawnFx(lane + Vector3.up * 0.02f, new Vector3(r * 0.6f, 0.02f, r * 1.6f), new Vector3(r * 1.4f, 0.02f, r * 3f), 0.25f, Gust, rot);
            yield return waitGap;
        }
    }
}
