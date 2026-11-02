using UnityEngine;

/// <summary>능력이 생명체/게임 상태에 접근하는 창구.</summary>
public class CreatureContext
{
    public readonly Ward Ward;
    public readonly CreatureDef Def;

    public CreatureContext(Ward ward, CreatureDef def) { Ward = ward; Def = def; }

    public PlayerStats Stats => GameManager.I.Stats;
    public StageDirector Stage => StageDirector.I;
    public Vector3 Position => Ward.Position;
    public Vector3 Heading => Ward.Heading;
}
