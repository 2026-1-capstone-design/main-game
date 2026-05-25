using System.Collections.Generic;
using UnityEngine;

public sealed class UnholyBlessingSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.UnholyBlessing;
    public skillType SkillCategory => skillType.support;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 100f;
    public float AreaRadius => 0f;

    // 시체(죽은 아군)가 있는지 확인
    public bool CanActivate(in BattleEffectContext context)
    {
        if (context.Actor == null) return false;
        foreach (var unit in context.Units)
        {
            // 아군이면서 현재 죽은 상태인 경우
            if (unit.State.TeamId == context.Actor.State.TeamId && unit.State.IsCombatDisabled) return true;
        }
        return false;
    }

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime?.State;
        if (casterState == null || casterState.IsCombatDisabled) return;

        BattleRuntimeUnit deadAlly = null;

        // 부활시킬 아군 탐색
        foreach (var unit in context.Units)
        {
            if (unit.State.TeamId == casterState.TeamId && unit.State.IsCombatDisabled)
            {
                deadAlly = unit;
                break;
            }
        }

        if (deadAlly != null)
        {
            // 최대 체력의 30%로 강제 부활
            float reviveHp = Mathf.Max(1f, deadAlly.State.MaxHealth * 0.3f);
            effects.Revive(deadAlly.State, reviveHp);

            // 이성을 잃어 스킬 사용 영구 불가 처리 (Duration을 9999f로 무한 유지)
            effects.ApplyStatus(new BattleStatusRequest
            {
                Source = casterState, Target = deadAlly.State,
                Type = BattleStatusType.SkillDisabled,
                Level = 1, Duration = 9999f, IsDebuff = true, IsDispelAllowed = false // 해제 불가
            });

            // 덤으로 받는피해 대폭 증가 디버프 등을 걸어 30% 능력치 페널티를 시뮬레이션할 수도 있습니다.
            effects.ApplyStatus(new BattleStatusRequest
            {
                Source = casterState, Target = deadAlly.State,
                Type = BattleStatusType.DamageTakenPercent,
                Level = 70, // 받는 피해 70% 증가 (몸빵 약화)
                Duration = 9999f, IsDebuff = true, IsDispelAllowed = false
            });

            VFXManager.Instance.PlayEffect("DarkHealEffect", deadAlly.State.Position);
        }
    }
}
