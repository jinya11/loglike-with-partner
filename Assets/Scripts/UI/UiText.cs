/// <summary>프리팹에서 사용자가 편집한 서식 문구로 안전하게 포맷 ({n} 번호가 맞지 않아도 예외 대신 원문 표시).</summary>
public static class UiText
{
    public static string Format(string fmt, params object[] args)
    {
        try { return string.Format(fmt, args); }
        catch (System.FormatException) { return fmt; }
    }

    /// <summary>키 표시 이름 (Digit1 → 1).</summary>
    public static string KeyName(UnityEngine.InputSystem.Key k)
    {
        string s = k.ToString();
        return s.StartsWith("Digit") ? s.Substring(5) : s;
    }

    /// <summary>클릭 가능한 UI용: 씬에 EventSystem이 없으면 하나 둠 (씬에 이미 있으면 아무것도 안 함).</summary>
    public static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        new UnityEngine.GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }
}
