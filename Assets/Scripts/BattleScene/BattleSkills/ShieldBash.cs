using System.Collections.Generic;
using UnityEngine;

// 11. 방패 밀치기 (쉴드) : 강한 넉백 및 적 공격력 감소
public sealed class ShieldBashSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.ShieldBash;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.shield };

    public bool CanActivate(BattleRuntimeUnit caster) =>
        caster.PlannedTargetEnemy != null
        && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(caster.State, caster.PlannedTargetEnemy);

    public void Apply(BattleRuntimeUnit caster, ISkillEffectApplier applier)
    {
        var target = caster.PlannedTargetEnemy;
        if (target == null)
            return;

        applier.ApplyDamage(target, caster.Attack * 1.0f);
        Vector3 pushDir = target.Position - caster.Position;
        applier.AddKnockback(target, pushDir, 120f); // 강력한 넉백

        // 공격력 디버프 (음수 레벨 부여)
        applier.ApplyBuff(target, BuffType.AttackDamage, -3, 6f);
    }
}
