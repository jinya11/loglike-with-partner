using System;
using UnityEngine;

/// <summary>효과음 하나: 클립 + 음량 + 연속 재생 최소 간격. 클립을 비우면 소리 없음.</summary>
[Serializable]
public class Sfx
{
    public AudioClip clip;
    [Range(0, 1)] public float volume = 0.8f;
    [Tooltip("같은 소리 최소 간격(초). 많은 적이 동시에 맞을 때 겹침 방지")] [Min(0)] public float cooldown = 0.04f;
    [NonSerialized] public float last = -99f;

    public Sfx() { }
    public Sfx(float volume, float cooldown = 0.04f) { this.volume = volume; this.cooldown = cooldown; }
}

/// <summary>생명체 하나의 효과음. creature = WardConfig.creatures의 name.</summary>
[Serializable]
public class CreatureSfx
{
    [Tooltip("WardConfig 생명체 이름 (예: 토끼)")] public string creature;
    [Tooltip("공격 발사 (비우면 무음)")] public Sfx attack = new Sfx(0.3f, 0.08f);
    [Tooltip("던진 물체가 땅에 닿을 때 (토끼 당근 등)")] public Sfx impact = new Sfx(0.45f, 0.06f);
    public Sfx skill = new Sfx(0.7f);
    public Sfx hurt = new Sfx(0.7f, 0.1f);
    public Sfx die = new Sfx(0.8f);
}

/// <summary>효과음 설정 (Resources/Config/SfxConfig). 효과음마다 클립/음량을 여기서 직접 바꿈.
/// 메뉴 Roguelike/Build Sfx Config: 비어 있는 클립만 Assets/Audio/Sfx 파일(필드 이름의 스네이크 표기)로 채움.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Sfx")]
public class SfxConfig : ScriptableObject
{
    [Header("전체")]
    [Range(0, 1)] public float masterVolume = 0.8f;
    [Tooltip("카메라에서 이 거리(m) 밖의 소리는 점점 작아짐")] public float hearRange = 18f;

    [Header("캐릭터 - 생명체별")]
    public CreatureSfx[] creatures;

    [Header("캐릭터 - 감시자 / 플레이어 / 적")]
    public Sfx watcherStrike = new Sfx(0.6f, 0.08f);
    [Tooltip("폭격(일반·3중) 한 발이 땅에 닿을 때마다")] public Sfx bombardImpact = new Sfx(0.6f, 0f);
    public Sfx playerHurt = new Sfx(0.7f, 0.1f);
    public Sfx enemyHit = new Sfx(0.35f, 0.06f);
    public Sfx enemyDie = new Sfx(0.5f, 0.05f);

    [Header("환경")]
    [Tooltip("배경 루프")] public Sfx wind = new Sfx(0.15f, 0f);
    [Tooltip("모래폭풍 중 배경 루프")] public Sfx sandstorm = new Sfx(0.8f, 0f);
    public Sfx portalAppear = new Sfx(0.8f, 0.3f);
    public Sfx groundCollapse = new Sfx(0.7f, 0.5f);
    public Sfx iceCrack = new Sfx(0.6f, 0.15f);
    public Sfx iceBreak = new Sfx(0.7f, 0.1f);

    [Header("기타")]
    public Sfx xpPickup = new Sfx(0.3f, 0.05f);
    public Sfx levelUp = new Sfx(0.8f, 0.3f);
    public Sfx resourcePickup = new Sfx(0.7f, 0.05f);
    [Tooltip("모든 UI 버튼 클릭")] public Sfx uiClick = new Sfx(0.6f, 0.03f);
    [Tooltip("상호작용(F)으로 창 열기")] public Sfx uiOpen = new Sfx(0.6f, 0.1f);
    public Sfx gameOver = new Sfx(0.8f, 0.5f);
    public Sfx victory = new Sfx(0.8f, 0.5f);

    public CreatureSfx Creature(string name)
    {
        if (creatures != null && !string.IsNullOrEmpty(name)) foreach (var c in creatures) if (c != null && c.creature == name) return c;
        return null;
    }
}
