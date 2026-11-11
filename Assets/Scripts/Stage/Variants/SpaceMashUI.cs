using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스페이스 연타 UI (Canvas 프리팹 IceEscape, 얼어붙은 호수 씬에 배치 — 위치/색/크기는 프리팹이나 씬에서 편집).
/// 화면 중앙: 스페이스 키 모양(누를 때마다 눌림) + 탈출 게이지 + 남은 시간 막대. 문장 없음.
/// FrozenLakeVariant가 생명체가 빠져 있는 동안 매 프레임 Show, 아니면 Hide.
/// </summary>
public class SpaceMashUI : MonoBehaviour
{
    [Tooltip("전체 (투명도로 켜고 끔)")] public CanvasGroup group;
    [Tooltip("키 모양 (누르면 작아졌다 돌아옴)")] public RectTransform keycap;
    [Tooltip("키 바탕")] public Image keyBack;
    [Tooltip("탈출 게이지 (Image Filled Horizontal)")] public Image progress;
    [Tooltip("남은 시간 막대 (Image Filled Horizontal)")] public Image timeBar;
    [Tooltip("누른 횟수 / 필요 횟수")] public TMP_Text counter;
    public Color keyColor = new Color(0.85f, 0.95f, 1f), pressColor = new Color(0.55f, 0.85f, 1f);
    public Color progressColor = new Color(0.45f, 0.9f, 1f), timeColor = new Color(1f, 0.55f, 0.4f);

    float punch, shown;

    void Awake() => Hide();

    public void Hide()
    {
        shown = 0f;
        if (group) { group.alpha = 0f; group.gameObject.SetActive(false); }
    }

    public void Show(int presses, int need, float timeLeft01, bool pressed)
    {
        if (!group) return;
        if (!group.gameObject.activeSelf) group.gameObject.SetActive(true);
        float dt = Time.unscaledDeltaTime;
        shown = Mathf.MoveTowards(shown, 1f, dt * 6f);
        group.alpha = shown;
        if (pressed) punch = 1f;
        punch = Mathf.MoveTowards(punch, 0f, dt * 8f);
        // 아무것도 안 누를 때는 재촉하듯 살짝 통통
        float idle = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 14f);
        if (keycap) keycap.localScale = Vector3.one * (punch > 0f ? 1f - 0.12f * punch : idle);
        if (keyBack) keyBack.color = Color.Lerp(keyColor, pressColor, punch);
        if (progress) { progress.fillAmount = need > 0 ? Mathf.Clamp01(presses / (float)need) : 1f; progress.color = progressColor; }
        if (timeBar) { timeBar.fillAmount = Mathf.Clamp01(timeLeft01); timeBar.color = timeColor; }
        if (counter) counter.text = $"{presses}/{need}";
    }
}
