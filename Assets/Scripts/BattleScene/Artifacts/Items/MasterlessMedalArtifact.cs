using UnityEngine;

// 주인 잃은 훈장: 스킬 재사용 시간이 % 감소하지만, 평타 데미지 % 감소
// 수치 설정: 스킬 시전 직후 레벨당 15%(1Lv: 15%, 2Lv: 30%, 3Lv: 45%)만큼의 스킬 전체 쿨타임을 즉시 차감하여 재사용 시간을 줄여주며, 그 대가로 기본 공격(평타)의 피해량이 레벨당 10% 감소하도록 설정했습니다.
public sealed class MasterlessMedalArtifact : ISkillCastReactionArtifact, IDamageModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.MasterlessMedal;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void OnSkillCast(BattleUnitCombatState owner, in BattleSkillCastEvent skillCastEvent, IBattleEffectSink effects)
    {
        if (skillCastEvent.Caster == owner)
        {
            // ResetSkillCooldown 이후에 호출되는 훅이므로, 전체 쿨타임의 퍼센트만큼 시간을 즉시 감소시킵니다.
            float refundTime = owner.SkillCooltime * (_level * 0.15f);
            owner.TickSkillCooldown(refundTime);
        }
    }

    public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
    {
        if (request.Source == owner && request.IsBasicAttack)
        {
            float damagePenalty = _level * 0.1f;
            request.Amount *= Mathf.Max(0f, 1f - damagePenalty);
        }
    }
}
