using TMPro;
using UnityEngine;

/// <summary>보유 자원 표시. 서식: {0}=자원 이름, {1}=개수.</summary>
public class ResourceLabel : MonoBehaviour
{
    [SerializeField] ResourceType type;
    [SerializeField] TMP_Text text;
    string fmt;

    void Awake() => fmt = text.text;
    void OnEnable() { SaveData.ResourceChanged += OnChanged; Refresh(); }
    void OnDisable() => SaveData.ResourceChanged -= OnChanged;
    void OnChanged(ResourceType t, int v) { if (t == type) Refresh(); }

    public void Refresh()
    {
        if (fmt == null) fmt = text.text;
        var def = GameManager.I ? GameManager.I.Config.Resource(type) : null;
        text.text = UiText.Format(fmt, def != null ? def.name : type.ToString(), SaveData.Get(type));
    }
}
