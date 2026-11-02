using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 로비 씬이 열리면 생명체 목록(WardConfig.creatures: 토끼, 마녀 엘리, 새 소녀 유나 …)의 모델을 플레이어 근처에 꺼내
/// 자유롭게 걸어다니게 함 (LobbyWanderer). 다가가서 상호작용(F)하면 전투에 데려갈지 묻는 창(CreatureSelectUI).
/// 씬을 수정하지 않고 실행 중에만 생성.
/// </summary>
public static class LobbyCreatures
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Spawn(SceneManager.GetActiveScene()); // 처음 로드된 씬은 sceneLoaded가 이미 지나감
    }

    static void OnLoaded(Scene s, LoadSceneMode m) => Spawn(s);

    static void Spawn(Scene s)
    {
        var gm = GameManager.I;
        if (gm == null || s.name != gm.Config.stage.lobbyScene || PlayerController.I == null) return;
        var list = gm.Config.ward.creatures;
        if (list == null) return;
        Vector3 c = PlayerController.I.transform.position;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] == null || !list[i].model) continue;
            float a = (i + 0.5f) / list.Length * Mathf.PI * 2f;
            var root = new GameObject(list[i].name + "_Lobby");
            SceneManager.MoveGameObjectToScene(root, s);
            root.transform.position = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.5f;
            var model = Object.Instantiate(list[i].model, root.transform);
            model.name = "Visual";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var w = root.AddComponent<LobbyWanderer>();
            w.home = c;
            w.visual = model.transform;
            root.AddComponent<CreaturePick>().index = i;
        }
    }
}

/// <summary>로비 생명체: 다가가서 F → 전투에 데려갈지 묻는 창.</summary>
public class CreaturePick : Interactable
{
    public int index;

    CreatureDef Def => GameManager.I.Config.ward.creatures[index];
    public bool Selected => GameManager.I.CreatureIndexEffective == index;

    public override string Prompt => Selected ? $"[F] {Def.name}  <color=#9fe8a0>(함께 출발)</color>" : $"[F] {Def.name}와 이야기하기";

    public override void Interact() => CreatureSelectUI.Open(index);
}
