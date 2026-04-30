using System.Collections.Generic;
using UnityEngine;

// 4. 목긋기 (단검) : 적 뒤로 이동 후 데미지
public sealed class ThroatSlitSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.ThroatSlit;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.dagger };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) =>
        context.Actor != null && context.Actor.PlannedTargetEnemy != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        BattleRuntimeUnit target = context.PrimaryTarget;
        if (target == null)
            return;

        // 적의 뒤편으로 위치 계산
        Vector3 dirToTarget = (target.Position - caster.Position).normalized;
        Vector3 behindPos = target.Position + dirToTarget * 2f;
        behindPos.y = caster.Position.y;

        caster.SetPosition(behindPos); // 텔레포트
        effects.DealDamage(
            new BattleDamageRequest
            {
                Source = caster,
                Target = target,
                Amount = caster.Attack * 2.0f,
                SourceKind = BattleEffectSourceKind.Skill,
                DamageKind = BattleDamageKind.Direct,
                SkillId = SkillId,
                IsSkill = true,
            }
        );
    }
}
