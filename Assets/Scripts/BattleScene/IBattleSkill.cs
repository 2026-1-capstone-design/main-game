using System.Collections.Generic;

// 하나의 스킬 전체 정의를 담는 인터페이스.
// 새 스킬 추가 시 이 인터페이스를 구현하는 클래스 1개만 추가하면 된다.
public interface IBattleSkill
{
    WeaponSkillId SkillId { get; }
    skillType SkillCategory { get; }

    // 이 스킬을 장착할 수 있는 무기 타입 목록. 비어 있으면 모든 무기에 호환.
    IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; }
    BattleSkillTargetPolicy TargetPolicy { get; }
    float CastRange { get; }
    float AreaRadius { get; }

    // 스킬 발동 조건 확인
    bool CanActivate(in BattleEffectContext context);

    // 스킬 효과 적용 (비주얼/쿨다운 리셋은 호출자가 담당)
    void Activate(in BattleEffectContext context, IBattleEffectSink effects);
}
