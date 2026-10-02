using UnityEngine;

/// <summary>월드 공간 UI가 항상 카메라를 향하도록 (위치는 월드에 고정).</summary>
public class Billboard : MonoBehaviour
{
    Transform cam;

    void LateUpdate()
    {
        if (cam == null) { var c = Camera.main; if (c == null) return; cam = c.transform; }
        transform.rotation = cam.rotation;
    }
}
