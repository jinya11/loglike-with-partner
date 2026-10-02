using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 동반자 선택 장소. F로 들어가면 클로즈업 카메라로 전환해 동반자를 화면 왼쪽에 보여주고 능력 설명 표시.
/// ←/→(A/D) 다른 동반자, Enter 선택, Esc 취소. 텍스트는 HUD_CompanionSelect 프리팹 문구를 서식으로 사용.
/// </summary>
public class CompanionStation : Interactable
{
    [SerializeField] string prompt = "[F] 동반자 고르기";
    [Header("Close-up")]
    [SerializeField] Camera showcaseCamera;
    [Tooltip("미리보기 동반자가 놓일 위치 (카메라 기준 화면 왼쪽에 보이도록 배치)")]
    [SerializeField] Transform previewAnchor;
    [SerializeField] float spinSpeed = 20f;
    [Header("UI (HUD_CompanionSelect)")]
    [SerializeField] GameObject panel;
    [Tooltip("서식: {0}=이름")] [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text abilityText;
    [Tooltip("서식: {0}=필살기 이름, {1}=설명")] [SerializeField] TMP_Text ultimateText;
    [Tooltip("서식: {0}=현재 번호, {1}=전체 수")] [SerializeField] TMP_Text indexText;
    [SerializeField] GameObject currentBadge;
    [Tooltip("선택 화면 동안 숨길 다른 UI")] [SerializeField] Canvas[] hideCanvases;

    string nameFmt, ultFmt, indexFmt;
    Camera mainCam;
    GameObject preview;
    int index, openedFrame = -1;
    bool open;

    public override string Prompt => prompt;

    void Awake()
    {
        nameFmt = nameText.text; ultFmt = ultimateText.text; indexFmt = indexText.text;
        panel.SetActive(false);
        showcaseCamera.enabled = false;
    }

    public override void Interact()
    {
        var gm = GameManager.I;
        open = true;
        openedFrame = Time.frameCount;
        gm.InputLocked = true;
        mainCam = Camera.main;
        if (mainCam) mainCam.enabled = false;
        showcaseCamera.enabled = true;
        foreach (var c in hideCanvases) c.enabled = false;
        panel.SetActive(true);
        index = gm.CompanionIndex;
        Show();
    }

    void Update()
    {
        if (!open) return;
        if (preview) preview.transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        var kb = Keyboard.current;
        if (kb == null || Time.frameCount == openedFrame) return;

        var list = GameManager.I.Config.companions;
        if (kb.escapeKey.wasPressedThisFrame) Close();
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            GameManager.I.CompanionIndex = index;
            LobbyInteractable.RefreshAll();
            Close();
        }
        else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) { index = (index + list.Length - 1) % list.Length; Show(); }
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) { index = (index + 1) % list.Length; Show(); }
    }

    void Show()
    {
        var list = GameManager.I.Config.companions;
        var def = list[index];
        if (preview) Destroy(preview);
        if (def.prefab)
        {
            preview = Instantiate(def.prefab, previewAnchor.position, previewAnchor.rotation, previewAnchor);
            var logic = preview.GetComponent<CompanionBase>();
            if (logic) DestroyImmediate(logic); // 미리보기는 모습만
            preview.transform.localScale = def.prefab.transform.localScale * def.previewScale;
        }
        nameText.text = string.Format(nameFmt, def.name);
        abilityText.text = def.abilityText;
        ultimateText.text = string.Format(ultFmt, def.ultimateName, def.ultimateDesc);
        indexText.SetText(indexFmt, index + 1, list.Length);
        currentBadge.SetActive(index == GameManager.I.CompanionIndex);
    }

    void Close()
    {
        open = false;
        if (preview) Destroy(preview);
        panel.SetActive(false);
        showcaseCamera.enabled = false;
        if (mainCam) mainCam.enabled = true;
        foreach (var c in hideCanvases) c.enabled = true;
        GameManager.I.InputLocked = false;
    }
}
