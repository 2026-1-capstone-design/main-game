using System.Collections.Generic;

// DefaultHealSkill: 스킬 없음(WeaponSkillId.None) 또는 미구현 스킬의 fallback.
// 자신에게 힐 10 적용.
public sealed class DefaultHealSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.None;
    public skillType SkillCategory => skillType.support;

    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new WeaponType[0];
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        effects.Heal(
            new BattleHealRequest
            {
                Source = context.Actor != null ? context.Actor.State : null,
                Target = context.Actor != null ? context.Actor.State : null,
                Amount = 10f,
                SourceKind = BattleEffectSourceKind.Skill,
            }
        );
    }
}
