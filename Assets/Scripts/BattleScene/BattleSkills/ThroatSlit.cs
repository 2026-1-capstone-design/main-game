using System.Collections.Generic;
using UnityEngine;

// 4. 목긋기 (단검) : 적 뒤로 이동 후 데미지
public sealed class ThroatSlitSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.ThroatSlit;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.dagger };

    public bool CanActivate(BattleRuntimeUnit caster) => caster.PlannedTargetEnemy != null;

    public void Apply(BattleRuntimeUnit caster, ISkillEffectApplier applier)
    {
        var target = caster.PlannedTargetEnemy;
        if (target == null)
            return;

        // 적의 뒤편으로 위치 계산
        Vector3 dirToTarget = (target.Position - caster.Position).normalized;
        Vector3 behindPos = target.Position + dirToTarget * 2f;
        behindPos.y = caster.Position.y;

        caster.SetPosition(behindPos); // 텔레포트
        applier.ApplyDamage(target, caster.Attack * 2.0f);
    }
}
