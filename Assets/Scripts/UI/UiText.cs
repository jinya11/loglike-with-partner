/// <summary>프리팹에서 사용자가 편집한 서식 문구로 안전하게 포맷 ({n} 번호가 맞지 않아도 예외 대신 원문 표시).</summary>
public static class UiText
{
    public static string Format(string fmt, params object[] args)
    {
        try { return string.Format(fmt, args); }
        catch (System.FormatException) { return fmt; }
    }
}
