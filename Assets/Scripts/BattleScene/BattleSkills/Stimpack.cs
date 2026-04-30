using System.Collections.Generic;

// 8. 스팀팩 (라이플) : 자신의 공속 대폭 상승

public sealed class StimpackSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Stimpack;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.rifle };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        effects.ApplyBuff(context.Actor, context.Actor, BuffType.AttackSpeed, 4, 8f);
    }
}
