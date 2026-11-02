using UnityEngine;

/// <summary>어두운 지역(심연)에서 플레이어 / 작은 생명체를 따라다니는 조명.</summary>
public class FollowLight : MonoBehaviour
{
    public enum Target { Player, Ward }
    [SerializeField] Target target;
    [SerializeField] Vector3 offset = new Vector3(0f, 4f, 0f);

    void LateUpdate()
    {
        Transform t = target == Target.Ward ? (Ward.I ? Ward.I.transform : null) : (PlayerController.I ? PlayerController.I.transform : null);
        if (t) transform.position = t.position + offset;
    }
}
