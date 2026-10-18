using System.Collections;
using UnityEngine;

/// <summary>풍신: 생명체 주변 적이 몰린 쪽으로 돌풍을 일으켜 적들을 한쪽으로 멀리 밀어냄 (약한 피해).</summary>
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
            // 생명체 주변에 몰린 적을 생명체에서 멀어지는 방향으로 밀어냄
            Vector3 p = Ward.I != null ? Ward.I.Position : PlayerController.I.Position; p.y = 0f;
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

    /// <summary>폭격 (풍신): 지정 위치에 회오리를 세워 주변 적을 빨아들이며 깎다가, 마지막에 사방으로 날려버림.</summary>
    protected override IEnumerator Bombard(Vector3 target, PlayerStats s)
    {
        var stage = StageDirector.I;
        float R = bombardRadius * s.areaMul, D = R * 2f;
        var tick = new WaitForSeconds(0.25f);
        stage.SpawnFx(target + Vector3.up * 0.03f, new Vector3(D, 0.02f, D), new Vector3(D * 0.9f, 0.02f, D * 0.9f), 0.6f, Warn);
        yield return new WaitForSeconds(0.6f);

        for (int i = 0; i < 12; i++) // 3초간 흡입
        {
            stage.RadialForce(target, R, -12f, s.companionDamage * 0.25f);
            float h = 3f + (i % 3);
            stage.SpawnFx(target + Vector3.up * h, new Vector3(R * 0.5f, h, R * 0.5f), new Vector3(R * 0.15f, h, R * 0.15f), 0.25f, Gust, Quaternion.Euler(0f, i * 40f, 0f));
            stage.SpawnFx(target + Vector3.up * 0.04f, new Vector3(D, 0.02f, D), new Vector3(D * 0.3f, 0.02f, D * 0.3f), 0.25f, Warn);
            yield return tick;
        }
        stage.RadialForce(target, R * 1.2f, 40f, s.companionDamage * 2f); // 마지막 폭발
        stage.SpawnFx(target + Vector3.up * 0.05f, new Vector3(1f, 0.02f, 1f), new Vector3(D * 1.6f, 0.02f, D * 1.6f), 0.35f, Gust);
    }
}