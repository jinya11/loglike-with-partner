using UnityEngine;

/// <summary>화면 분할 '작은 화면'(오른쪽 아래) 수치. Resources/Config/ScreenSplitConfig.asset.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/ScreenSplit")]
public class ScreenSplitConfig : ScriptableObject
{
    static ScreenSplitConfig cached;
    public static ScreenSplitConfig I => cached ? cached : (cached = Resources.Load<ScreenSplitConfig>("Config/ScreenSplitConfig") ?? CreateInstance<ScreenSplitConfig>());

    [Tooltip("작은 화면 크기 (화면 대비 비율, 가로세로 같은 비율 → 화면과 같은 종횡비)")] [Range(0.1f, 0.6f)] public float pipSize = 0.3f;
    [Tooltip("오른쪽/아래 여백 (화면 높이 대비 비율)")] [Range(0f, 0.2f)] public float pipMargin = 0.025f;
    [Tooltip("테두리 두께 (px, 1920x1080 기준)")] public float borderWidth = 6f;
    public Color borderColor = new Color(1f, 0.85f, 0.9f, 1f);
    [Tooltip("작은 화면이 떠 있을 때 생명체 명령 패널(CommandHud)을 작은 화면 위로 올리는 간격 (UI 단위, 1920x1080 기준)")] public float hudGap = 16f;

    /// <summary>작은 화면 viewport (정규화). 여백은 높이 기준 → 가로 여백은 종횡비로 환산.</summary>
    public Rect PipRect(float aspect)
    {
        float my = pipMargin, mx = pipMargin / Mathf.Max(0.1f, aspect);
        return new Rect(1f - mx - pipSize, my, pipSize, pipSize);
    }
}
