using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 로비 생명체와 상호작용하면 뜨는 창: "이 생명체를 전투에 데려갈까요?" → 상호작용 키(F)/Enter/버튼으로 데려가기, Esc로 닫기.
/// 이미 함께 출발하는 생명체면 그렇다고 알려 줌. 코드로 만든 작은 uGUI (처음 열 때 생성).
/// </summary>
public class CreatureSelectUI : MonoBehaviour
{
    static CreatureSelectUI inst;

    GameObject panel;
    TMP_Text title, desc, current, confirmLabel;
    int index, openedFrame;

    public static bool IsOpen => inst && inst.panel.activeSelf;

    public static void Open(int index)
    {
        if (!inst) inst = new GameObject("CreatureSelectUI").AddComponent<CreatureSelectUI>();
        inst.Show(index);
    }

    void Awake() => Build();

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var cs = gameObject.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var p = Rect("Panel", transform, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(760, 300));
        p.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.065f, 0.08f, 0.92f);
        panel = p.gameObject;
        title = Text("Title", p, 40, new Color(0.95f, 0.8f, 0.5f), new Vector2(0, -28), new Vector2(700, 56));
        desc = Text("Desc", p, 24, new Color(0.85f, 0.83f, 0.88f), new Vector2(0, -92), new Vector2(700, 70));
        current = Text("Current", p, 20, new Color(0.62f, 0.9f, 0.63f), new Vector2(0, -160), new Vector2(700, 34));

        var ok = Rect("Confirm", p, new Vector2(0.5f, 0f), new Vector2(-150, 28), new Vector2(270, 64));
        ok.gameObject.AddComponent<Image>().color = new Color(0.55f, 0.42f, 0.2f);
        ok.gameObject.AddComponent<Button>().onClick.AddListener(Confirm);
        confirmLabel = Text("Label", ok, 24, Color.white, Vector2.zero, new Vector2(270, 64), true);
        var no = Rect("Close", p, new Vector2(0.5f, 0f), new Vector2(150, 28), new Vector2(270, 64));
        no.gameObject.AddComponent<Image>().color = new Color(0.25f, 0.23f, 0.28f);
        no.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        Text("Label", no, 24, Color.white, Vector2.zero, new Vector2(270, 64), true).text = "그만두기 (Esc)";
        panel.SetActive(false);
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, anchor.y > 0.5f ? 1f : 0f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static TMP_Text Text(string name, Transform parent, int size, Color c, Vector2 pos, Vector2 box, bool centerInParent = false)
    {
        var rt = Rect(name, parent, centerInParent ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f), pos, box);
        if (centerInParent) rt.pivot = new Vector2(0.5f, 0.5f);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.fontStyle = FontStyles.Bold;
        return t;
    }

    void Show(int i)
    {
        var gm = GameManager.I;
        var defs = gm.Config.ward.creatures;
        if (defs == null || i < 0 || i >= defs.Length) return;
        index = i;
        var d = defs[i];
        bool selected = gm.CreatureIndexEffective == i;
        title.text = selected ? $"{d.name} — 함께 출발해요" : $"{d.name}를 전투에 데려갈까요?";
        desc.text = d.desc;
        var cur = defs[Mathf.Clamp(gm.CreatureIndexEffective, 0, defs.Length - 1)];
        current.text = $"지금 함께 출발하는 생명체: {cur.name}";
        confirmLabel.text = selected ? "확인 (F)" : "데려가기 (F)";
        panel.SetActive(true);
        gm.InputLocked = true;
        openedFrame = Time.frameCount;
    }

    void Confirm()
    {
        var gm = GameManager.I;
        if (gm.CreatureIndexEffective != index)
        {
            gm.CreatureIndex = index;
            if (PlayerController.I) Vfx.Pillar(PlayerController.I.Position, new Color(0.6f, 1f, 0.6f));
        }
        Close();
    }

    void Close()
    {
        panel.SetActive(false);
        GameManager.I.InputLocked = false;
        EventSystem.current?.SetSelectedGameObject(null);
    }

    void Update()
    {
        if (!panel.activeSelf || Time.frameCount == openedFrame) return; // 연 프레임의 F로 바로 확정되지 않게
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) Close();
        else if (kb.fKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Confirm();
    }
}
