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

    //현재 사용 가능 한지 상태 판단
    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    //사용할 떄 어떤 효과를 적용할 지 조건과 변수와 결과를 담아서 전달.
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
