using UnityEngine;

// 떠 있는 섬/바위가 천천히 오르내리며 살짝 흔들리게 한다.
public class FloatBob : MonoBehaviour
{
    public float amplitude = 0.3f;
    public float period = 6f;
    public float tiltDegrees = 0.6f;
    public float phase;

    Vector3 basePos;
    Quaternion baseRot;

    void Awake()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
        if (phase == 0f) phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        float t = Time.time * Mathf.PI * 2f / period + phase;
        transform.localPosition = basePos + Vector3.up * Mathf.Sin(t) * amplitude;
        transform.localRotation = baseRot * Quaternion.Euler(Mathf.Sin(t * 0.7f) * tiltDegrees, 0f, Mathf.Cos(t * 0.5f) * tiltDegrees);
    }
}
