using UnityEngine;

// 로비 카메라가 목표 지점을 바라보며 아주 느리게 흔들리는 시네마틱 드리프트.
public class CameraDrift : MonoBehaviour
{
    public Transform target;
    public Vector3 lookOffset;
    public Vector3 swayAmount = new Vector3(0.8f, 0.35f, 0f);
    public float period = 18f;

    Vector3 basePos;

    void Awake() => basePos = transform.position;

    void LateUpdate()
    {
        float t = Time.time * Mathf.PI * 2f / period;
        transform.position = basePos + transform.right * Mathf.Sin(t) * swayAmount.x + Vector3.up * Mathf.Sin(t * 1.3f) * swayAmount.y;
        if (target != null) transform.LookAt(target.position + lookOffset);
    }
}
