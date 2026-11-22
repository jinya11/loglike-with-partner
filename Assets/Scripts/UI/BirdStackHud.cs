using TMPro;
using UnityEngine;

/// <summary>새 소녀 공속 스택 표시 (월드 공간 Canvas 프리팹 HUD_BirdStacks). BirdGrowth가 생성해 캐릭터 옆에 붙임.</summary>
public class BirdStackHud : MonoBehaviour
{
    [Tooltip("서식: {0}=스택")] [SerializeField] TMP_Text text;
    [Tooltip("캐릭터 기준 위치 (오른쪽 위)")] [SerializeField] Vector3 offset = new Vector3(0.9f, 1.6f, 0f);

    string fmt;
    int shown = -1;

    void Awake() => fmt = text.text;

    public void Set(int stacks)
    {
        if (stacks == shown) return; // 바뀔 때만 갱신
        shown = stacks;
        text.text = UiText.Format(fmt, stacks);
        gameObject.SetActive(stacks > 0);
    }

    /// <summary>카메라 기준 오른쪽에 둠 (Billboard가 회전 담당).</summary>
    public void Follow(Vector3 target, Transform cam)
    {
        Vector3 right = cam ? cam.right : Vector3.right;
        transform.position = target + right * offset.x + Vector3.up * offset.y;
    }
}
