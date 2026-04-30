using System.Collections.Generic;

// 3. 워크라이 (두손검) : 아군 전체 공격력 증가
public sealed class WarcrySkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Warcry;
    public skillType SkillCategory => skillType.support;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.twoHand };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.AreaAroundSelf;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        foreach (BattleRuntimeUnit unit in context.Units)
        {
            if (unit != null && !unit.IsCombatDisabled && unit.TeamId == caster.State.TeamId)
            {
                effects.ApplyBuff(caster, unit, BuffType.AttackDamage, 3, 10f); // 공격력 +30
            }
        }
    }
}
