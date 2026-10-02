using UnityEngine;

/// <summary>사이드(쿼터) 시점 추적 카메라. 오프셋 방향으로 고정 각도.</summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Vector3 offset = new Vector3(0f, 11f, -17f);
    [SerializeField] float smooth = 8f;

    void Start()
    {
        transform.rotation = Quaternion.LookRotation(-offset);
        if (PlayerController.I) transform.position = PlayerController.I.Position + offset;
    }

    void LateUpdate()
    {
        var p = PlayerController.I;
        if (p == null) return;
        transform.position = Vector3.Lerp(transform.position, p.Position + offset, 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime));
    }
}
