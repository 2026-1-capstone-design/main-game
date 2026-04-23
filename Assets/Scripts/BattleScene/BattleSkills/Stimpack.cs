using System.Collections.Generic;
using UnityEngine;

// 8. 스팀팩 (라이플) : 자신의 공속 대폭 상승

public sealed class StimpackSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Stimpack;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.rifle };

    public bool CanActivate(BattleRuntimeUnit caster, BattleFieldView field) => true;

    public void Apply(BattleRuntimeUnit caster, BattleFieldView field, ISkillEffectApplier applier)
    {
        applier.ApplyBuff(caster.State, BuffType.AttackSpeed, 4, 8f);
    }
}
