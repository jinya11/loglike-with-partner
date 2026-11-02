using UnityEngine;

/// <summary>
/// 보더 잠금 해제: 감시자(플레이어)가 바깥 보더 가까이 가면 [F]로 그 변에 새 구역을 붙임 (ArenaBorder.OpenSide).
/// 열린 구역은 지역별로 저장되어 계속 유지. 새 구역에서 만나는 생명체는 Unlocked 이벤트에 연결할 예정.
/// 비용은 아직 없음 (CanAfford/Pay 자리만, 재화 설정은 나중에). StageDirector가 보더 아래 자식 오브젝트로 추가
/// (상호작용 지점이 감시자를 따라 움직이므로 보더와 같은 오브젝트에 두지 않음).
/// </summary>
public class BorderUnlock : Interactable
{
    [Tooltip("보더에서 이 거리 안이면 열 수 있음")] public float reach = 5f;
    [Tooltip("새 구역이 다 열리기까지 (초)")] public float openTime = 2.5f;
    public Color unlockColor = new Color(0.6f, 0.9f, 1f);

    /// <summary>보더를 열기 시작했을 때 (새 구역 사각형 XZ, 열린 방향). 새 구역의 생명체 등장은 여기에 연결.</summary>
    public static event System.Action<Rect, Vector2Int> Unlocked;

    ArenaBorder border;
    int area;
    Vector2Int dir;
    bool available;

    void Awake() => border = GetComponentInParent<ArenaBorder>();

    static string DirName(Vector2Int d) => d == Vector2Int.up ? "북쪽" : d == Vector2Int.down ? "남쪽" : d == Vector2Int.right ? "동쪽" : "서쪽";

    public override string Prompt
    {
        get
        {
            if (!available) return "";
            if (!border.CanOpen(area, dir)) return border.Growing ? "보더가 열리는 중" : "더 이상 열 수 없는 보더";
            string c = CostText();
            return $"[F] {DirName(dir)} 보더 열기 (새 구역{(c.Length > 0 ? ", " + c : "")})";
        }
    }

    void LateUpdate()
    {
        // 감시자가 있는 구역에서 가장 가까운 바깥 변을 찾음. 가까우면 상호작용 지점을 감시자 위치로 (가장 가까운 대상이 되게)
        var p = PlayerController.I;
        available = false;
        if (p != null && GameManager.I.State == GameState.Playing)
        {
            Vector3 pos = p.Position;
            int a = border.AreaAt(pos);
            if (a >= 0)
            {
                var r = border.AreaRect(a);
                float best = reach;
                foreach (var d in ArenaBorder.Dirs)
                {
                    float dist = d == Vector2Int.up ? r.yMax - pos.z : d == Vector2Int.down ? pos.z - r.yMin : d == Vector2Int.right ? r.xMax - pos.x : pos.x - r.xMin;
                    if (dist > best) continue;
                    // 그 변 바로 바깥이 이미 열린 곳이면 바깥 보더가 아님
                    Vector3 probe = pos + new Vector3(d.x, 0f, d.y) * (dist + 1f);
                    if (border.AreaAt(probe) >= 0) continue;
                    best = dist; area = a; dir = d; available = true;
                }
            }
            if (available) { transform.position = new Vector3(pos.x, 0f, pos.z); return; }
        }
        transform.position = new Vector3(0f, -1000f, 0f);
    }

    public override void Interact()
    {
        if (!available || !CanAfford()) return;
        var rect = border.NewAreaRect(area, dir);
        if (!border.OpenSide(area, dir, openTime)) return;
        Pay();
        var pp = PlayerController.I.Position;
        Vfx.Pillar(new Vector3(pp.x, 0f, pp.z), unlockColor);
        Unlocked?.Invoke(rect, dir);
    }

    // ---- 비용 (재화 설정은 나중에) ----
    protected virtual bool CanAfford() => true;
    protected virtual void Pay() { }
    protected virtual string CostText() => "";
}
