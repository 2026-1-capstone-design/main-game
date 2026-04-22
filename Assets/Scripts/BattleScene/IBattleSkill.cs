using System.Collections.Generic;
using UnityEngine;

// 스킬 효과를 실제로 적용하는 실행자 인터페이스.
// SkillEffectApplier가 구현하며, 테스트 시에는 Test-double로 대체 가능.
public interface ISkillEffectApplier
{
    IEnumerable<BattleUnitCombatState> AllUnits { get; }
    void ApplyDamage(BattleUnitCombatState target, float amount);
    void AddKnockback(BattleUnitCombatState target, Vector3 direction, float force);
    void ApplyHeal(BattleUnitCombatState caster, float amount);
    void ApplyBuff(BattleUnitCombatState caster, BuffType type, int level, float duration);
}

// 하나의 스킬 전체 정의를 담는 인터페이스.
// 새 스킬 추가 시 이 인터페이스를 구현하는 클래스 1개만 추가하면 된다.
public interface IBattleSkill
{
    WeaponSkillId SkillId { get; }
    skillType SkillCategory { get; }

    // 이 스킬을 장착할 수 있는 무기 타입 목록. 비어 있으면 모든 무기에 호환.
    IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; }

    // 스킬 발동 조건 확인
    bool CanActivate(BattleRuntimeUnit caster, BattleFieldView field);

    // 스킬 효과 적용 (비주얼/쿨다운 리셋은 호출자가 담당)
    void Apply(BattleRuntimeUnit caster, BattleFieldView field, ISkillEffectApplier applier);
}
