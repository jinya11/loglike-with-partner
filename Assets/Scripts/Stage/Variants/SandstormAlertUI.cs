using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 모래폭풍 경보 UI (Canvas 프리팹 SandstormAlert, 전용 씬에 배치 — 위치/색/크기는 프리팹이나 씬에서 편집).
/// 경보: 화면 위 중앙 경고 아이콘 + 원형 게이지(폭풍까지 남은 시간) + 큰 숫자 + 바람에 밀릴 방향 화살표 + 화면 가장자리 경고색 펄스.
/// 폭풍 중: 같은 자리에 남은 시간 게이지/숫자 (색이 바뀜). 문장은 없음 (짧은 라벨 하나).
/// DesertSandstormVariant가 매 프레임 Show/Hide를 부름.
/// </summary>
public class SandstormAlertUI : MonoBehaviour
{
    [Tooltip("전체 (투명도로 켜고 끔)")] public CanvasGroup group;
    [Tooltip("짧은 라벨 (예: 모래폭풍)")] public TMP_Text label;
    [Tooltip("남은 초")] public TMP_Text count;
    [Tooltip("원형 게이지 (Image Filled Radial)")] public Image gauge;
    [Tooltip("경고 아이콘 바탕")] public Image icon;
    [Tooltip("바람에 밀릴 방향 화살표 (오른쪽이 0도)")] public RectTransform arrow;
    [Tooltip("화면 가장자리 펄스 (비네트)")] public Image edge;
    [Tooltip("위 중앙 묶음 (나타날 때 살짝 커졌다 돌아옴)")] public RectTransform panel;
    public Color warningColor = new Color(1f, 0.55f, 0.15f);
    public Color stormColor = new Color(0.95f, 0.3f, 0.2f);
    [Tooltip("가장자리 펄스 최대 투명도 (경보 / 폭풍)")] public float edgeAlphaWarning = 0.6f, edgeAlphaStorm = 0.7f;
    [Tooltip("깜빡임 속도")] public float pulseSpeed = 7f;

    float age, shown;

    void Awake() => Hide();

    public void Hide()
    {
        shown = 0f; age = 0f;
        if (group) { group.alpha = 0f; group.gameObject.SetActive(false); }
    }

    /// <summary>storm=false면 경보, true면 폭풍 중. fill01 = 게이지 남은 비율, seconds = 남은 초, arrowAngle = 화면 기준 도(0 = 오른쪽, 반시계).</summary>
    public void Show(bool storm, float fill01, float seconds, float arrowAngle)
    {
        if (!group) return;
        if (!group.gameObject.activeSelf) { group.gameObject.SetActive(true); age = 0f; }
        float dt = Time.unscaledDeltaTime;
        age += dt;
        shown = Mathf.MoveTowards(shown, 1f, dt * 4f);
        group.alpha = shown;
        Color c = storm ? stormColor : warningColor;
        float pulse = 0.5f + 0.5f * Mathf.Cos(age * pulseSpeed);
        if (label) label.color = Color.Lerp(Color.white, c, 0.25f); // 모래 배경에서도 잘 보이게 흰색 쪽
        if (count) { count.text = Mathf.CeilToInt(Mathf.Max(0f, seconds)).ToString(); count.color = Color.Lerp(Color.white, c, storm ? 0.3f : pulse); }
        if (gauge) { gauge.fillAmount = Mathf.Clamp01(fill01); gauge.color = c; }
        if (icon) icon.color = Color.Lerp(c, Color.white, storm ? 0f : pulse * 0.4f);
        if (arrow)
        {
            arrow.localRotation = Quaternion.Euler(0f, 0f, arrowAngle);
            foreach (var g in arrow.GetComponentsInChildren<Graphic>()) g.color = c;
            arrow.localScale = Vector3.one * (1f + 0.08f * pulse);
        }
        if (edge) { var e = c; e.a = (storm ? edgeAlphaStorm * (0.7f + 0.3f * pulse) : edgeAlphaWarning * pulse); edge.color = e; }
        // 처음 나타날 때 살짝 크게
        if (panel) panel.localScale = Vector3.one * (1f + Mathf.Max(0f, 0.3f - age) * 0.8f);
    }
}
