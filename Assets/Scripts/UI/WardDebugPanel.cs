using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 생명체 적 감지 디버그 상태창 (개발자 모드 전용, DevConfig로 설정).
/// 감지/도망/포위/저항 상태를 텍스트로, 감지 범위·경로·도망 목표·감지된 적을 월드 선으로 표시.
/// 개발자 모드가 아니면 오브젝트 자체를 만들지 않음.
/// </summary>
public class WardDebugPanel : MonoBehaviour
{
    const int CircleSeg = 48, MaxThreatLines = 24, WindowId = 0x5A11;

    DevConfig cfg;
    bool visible;
    float refreshLeft;
    readonly StringBuilder sb = new StringBuilder(1024);
    string text = "";
    GUIStyle style;
    Texture2D bg;
    LineRenderer fleeCircle, dangerCircle, surroundCircle, pulseCircle, pathLine, goalLine, threatLines;
    readonly Vector3[] buf = new Vector3[Mathf.Max(CircleSeg + 1, MaxThreatLines * 2 + 1)];
    readonly Vector3[] pathBuf = new Vector3[130];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (!DevConfig.Enabled) return;
        var go = new GameObject("[Dev] WardDebugPanel");
        DontDestroyOnLoad(go);
        go.AddComponent<WardDebugPanel>();
    }

    void Awake()
    {
        cfg = DevConfig.I;
        visible = cfg.wardDebugOnStart;
        var mat = new Material(Shader.Find("Sprites/Default"));
        fleeCircle = Line("Flee", mat, cfg.fleeRadiusColor, 0.06f);
        dangerCircle = Line("Danger", mat, cfg.dangerRadiusColor, 0.04f);
        surroundCircle = Line("Surround", mat, cfg.surroundRadiusColor, 0.05f);
        pulseCircle = Line("Pulse", mat, new Color(0.6f, 1f, 0.6f), 0.04f);
        pathLine = Line("Path", mat, cfg.pathColor, 0.08f);
        goalLine = Line("Goal", mat, Color.white, 0.04f);
        threatLines = Line("Threats", mat, new Color(1f, 0.5f, 0.2f, 0.7f), 0.03f);
        SetLines(false);
    }

    LineRenderer Line(string n, Material m, Color c, float w)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = m;
        lr.startColor = lr.endColor = c;
        lr.startWidth = lr.endWidth = w;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.positionCount = 0;
        return lr;
    }

    void SetLines(bool on)
    {
        fleeCircle.enabled = dangerCircle.enabled = surroundCircle.enabled = pulseCircle.enabled = on;
        pathLine.enabled = goalLine.enabled = threatLines.enabled = on;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[cfg.wardDebugKey].wasPressedThisFrame) visible = !visible;

        var w = Ward.I;
        bool active = visible && w != null && StageDirector.I != null;
        SetLines(active && cfg.drawWorldGizmos);
        if (!active) return;

        if (cfg.drawWorldGizmos) DrawWorld(w);
        if ((refreshLeft -= Time.unscaledDeltaTime) > 0f) return;
        refreshLeft = cfg.refreshInterval;
        BuildText(w);
    }

    void Circle(LineRenderer lr, Vector3 c, float r)
    {
        c.y = 0.08f;
        for (int i = 0; i <= CircleSeg; i++)
        {
            float a = i * (Mathf.PI * 2f / CircleSeg);
            buf[i] = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }
        lr.positionCount = CircleSeg + 1;
        lr.SetPositions(buf);
    }

    void DrawWorld(Ward w)
    {
        Vector3 p = w.Position;
        var stats = GameManager.I.Stats;
        Circle(fleeCircle, p, w.FleeRadius);
        Circle(dangerCircle, p, w.DangerRadius);
        Circle(surroundCircle, p, w.SurroundRadius);
        Circle(pulseCircle, p, stats.wardPulseRadius);

        // 남은 경로
        var path = w.Path;
        int n = 0;
        pathBuf[n++] = p + Vector3.up * 0.1f;
        for (int i = w.PathIndex; i < path.Count && n < pathBuf.Length; i++) pathBuf[n++] = path[i] + Vector3.up * 0.1f;
        pathLine.positionCount = n;
        pathLine.SetPositions(pathBuf);

        // 현재 목표 (도망 중이면 도망 목표)
        Vector3 g = w.Mode == Ward.AiMode.Flee && w.HasFleeGoal ? w.FleeGoal : w.CurrentGoal;
        goalLine.positionCount = 2;
        goalLine.SetPosition(0, p + Vector3.up * 1.2f);
        goalLine.SetPosition(1, g + Vector3.up * 0.1f);

        // 감지 반경 안의 적: 생명체 ↔ 적 별 모양 선
        float fr2 = w.FleeRadius * w.FleeRadius;
        var list = Enemy.Active;
        int k = 0;
        Vector3 c = p + Vector3.up * 0.5f;
        buf[k++] = c;
        for (int i = 0; i < list.Count && k < MaxThreatLines * 2; i++)
        {
            Vector3 e = list[i].transform.position;
            float dx = e.x - p.x, dz = e.z - p.z;
            if (dx * dx + dz * dz >= fr2) continue;
            buf[k++] = e + Vector3.up * 0.5f;
            buf[k++] = c;
        }
        threatLines.positionCount = k;
        threatLines.SetPositions(buf);
    }

    void BuildText(Ward w)
    {
        Vector3 p = w.Position;
        var stats = GameManager.I.Stats;
        float fr2 = w.FleeRadius * w.FleeRadius, dr2 = w.DangerRadius * w.DangerRadius, pr2 = stats.wardPulseRadius * stats.wardPulseRadius;
        int inFlee = 0, inDanger = 0, inPulse = 0;
        float nearest = float.MaxValue;
        string nearestName = "-";
        var list = Enemy.Active;
        for (int i = 0; i < list.Count; i++)
        {
            Vector3 d = list[i].transform.position - p; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < fr2) inFlee++;
            if (sq < dr2) inDanger++;
            if (sq < pr2) inPulse++;
            if (sq < nearest) { nearest = sq; nearestName = list[i].Def != null ? list[i].Def.name : "기본"; }
        }

        int sides = w.LastSurroundSides;
        int sideCount = (sides & 1) + ((sides >> 1) & 1) + ((sides >> 2) & 1) + ((sides >> 3) & 1);

        sb.Clear();
        sb.Append("<b>생명체 감지 디버그</b>  [").Append(cfg.wardDebugKey).Append("]\n");
        sb.Append("상태: <b>").Append(ModeName(w.Mode)).Append("</b>   위치 ").Append(p.x.ToString("F1")).Append(", ").Append(p.z.ToString("F1")).Append('\n');
        sb.Append("체력 ").Append(stats.wardHp.ToString("F0")).Append(" / ").Append(stats.wardMaxHp.ToString("F0")).Append('\n');
        if (w.Creature != null)
        {
            sb.Append("종류: ").Append(w.Creature.name);
            string ab = w.Ability ? (w.Ability.DebugStatus() ?? w.Ability.displayName) : "능력 없음";
            sb.Append("   ").Append(ab).Append('\n');
        }
        sb.Append("<b>감지</b>  (전체 적 ").Append(list.Count).Append(")\n");
        sb.Append(" 도망 반경 ").Append(w.FleeRadius.ToString("F1")).Append(" 안: ").Append(inFlee).Append(inFlee > 0 ? "  → 위협" : "").Append('\n');
        sb.Append(" 위험 반경 ").Append(w.DangerRadius.ToString("F1")).Append(" 안: ").Append(inDanger).Append('\n');
        sb.Append(" 가장 가까운 적: ").Append(list.Count > 0 ? Mathf.Sqrt(nearest).ToString("F1") : "-").Append(" (").Append(nearestName).Append(")\n");
        sb.Append("<b>도망 / 경로</b>\n");
        sb.Append(" 도망 목표: ").Append(w.HasFleeGoal ? $"{w.FleeGoal.x:F1}, {w.FleeGoal.z:F1}" : "없음").Append('\n');
        sb.Append(" 경로: ").Append(Mathf.Max(0, w.Path.Count - w.PathIndex)).Append(" 칸 남음   재계산까지 ").Append(Mathf.Max(0f, w.ReplanLeft).ToString("F2")).Append("s\n");
        if (w.KnockVelocity.sqrMagnitude > 0.01f) sb.Append(" 넉백 중: ").Append(w.KnockVelocity.magnitude.ToString("F1")).Append('\n');
        sb.Append("<b>포위 구제</b>\n");
        sb.Append(" 반경 ").Append(w.SurroundRadius.ToString("F1")).Append(" 안 적 ").Append(w.LastSurroundCount).Append(" / ").Append(w.SurroundCountNeed)
          .Append("   막힌 방향 ").Append(sideCount).Append(" / ").Append(w.SurroundSidesNeed).Append(" ").Append(Sides(sides)).Append('\n');
        sb.Append(" 포위 지속 ").Append(w.SurroundedFor.ToString("F2")).Append(" / ").Append(w.SurroundDelay.ToString("F1"))
          .Append("s   쿨타임 ").Append(w.RescueCooldownLeft.ToString("F1")).Append("s\n");
        sb.Append("<b>저항 파동</b>\n");
        sb.Append(" 반경 ").Append(stats.wardPulseRadius.ToString("F1")).Append(" 안 적 ").Append(inPulse)
          .Append("   충전 ").Append(Mathf.Min(w.PulseTimer, stats.wardPulseInterval).ToString("F1")).Append(" / ").Append(stats.wardPulseInterval.ToString("F1")).Append("s\n");
        sb.Append("<b>상태 효과</b>\n ");
        int before = sb.Length;
        if (w.ShieldLeft > 0f) sb.Append("보호막 ").Append(w.ShieldLeft.ToString("F1")).Append("s  ");
        if (w.FearLeft > 0f) sb.Append("공포 ").Append(w.FearLeft.ToString("F1")).Append("s  ");
        if (w.SlowLeft > 0f) sb.Append("감속x").Append(w.SlowMul.ToString("F2")).Append(' ').Append(w.SlowLeft.ToString("F1")).Append("s  ");
        if (w.BleedLeft > 0f) sb.Append("출혈 ").Append(w.BleedLeft.ToString("F1")).Append("s  ");
        if (sb.Length == before) sb.Append("없음");
        text = sb.ToString();
    }

    static string ModeName(Ward.AiMode m) => m switch
    {
        Ward.AiMode.Flee => "<color=#ff8866>도망</color>",
        Ward.AiMode.Wander => "배회",
        Ward.AiMode.Pause => "대기",
        Ward.AiMode.Portal => "<color=#aa88ff>포털 이동</color>",
        _ => "-",
    };

    static string Sides(int s) =>
        $"[{((s & 1) != 0 ? "+X" : "··")} {((s & 2) != 0 ? "-X" : "··")} {((s & 4) != 0 ? "+Z" : "··")} {((s & 8) != 0 ? "-Z" : "··")}]";

    /// <summary>화면 좌표(Input System, 왼쪽 아래 원점)가 상태창 위인지. 창을 끌 때 게임 클릭(폭격 확정 등)을 막는 데 사용.</summary>
    public static bool PointerOver(Vector2 screenPos)
    {
        var p = current;
        if (p == null || !p.visible || Ward.I == null) return false;
        return p.windowRect.Contains(new Vector2(screenPos.x, Screen.height - screenPos.y));
    }

    static WardDebugPanel current;
    Rect windowRect;
    GUIStyle windowStyle;

    void OnEnable() { current = this; windowRect = new Rect(cfg != null ? cfg.panelPosition : new Vector2(12f, 140f), Vector2.zero); }
    void OnDisable() { if (current == this) current = null; }

    void OnGUI()
    {
        if (!visible || Ward.I == null || string.IsNullOrEmpty(text)) return;
        if (style == null)
        {
            bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            bg.Apply();
            style = new GUIStyle(GUI.skin.label) { fontSize = cfg.fontSize, richText = true, wordWrap = false, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            style.normal.textColor = Color.white;
            windowStyle = new GUIStyle { padding = new RectOffset(7, 7, 5, 5) };
            windowStyle.normal.background = bg;
        }
        var content = new GUIContent(text);
        Vector2 size = style.CalcSize(content);
        windowRect.width = size.x + 14f;
        windowRect.height = size.y + 10f;
        // 화면 밖으로 나가지 않게
        windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, Screen.width - windowRect.width));
        windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Screen.height - windowRect.height));
        windowRect = GUI.Window(WindowId, windowRect, _ =>
        {
            GUI.Label(new Rect(7f, 5f, size.x, size.y), content, style);
            GUI.DragWindow(); // 창 아무 곳이나 잡고 끌기
        }, GUIContent.none, windowStyle);
    }
}
