using System.Collections.Generic;

// IBattleSkill 구현체를 WeaponSkillId로 조회하는 레지스트리.
// 등록되지 않은 SkillId는 DefaultHealSkill로 폴백.
public sealed class BattleSkillRegistry
{
    private readonly Dictionary<WeaponSkillId, IBattleSkill> _skills;
    private readonly IBattleSkill _default;

    public BattleSkillRegistry(IEnumerable<IBattleSkill> skills)
    {
        _default = new DefaultHealSkill();
        _skills = new Dictionary<WeaponSkillId, IBattleSkill>();

        foreach (IBattleSkill skill in skills)
            _skills[skill.SkillId] = skill;
    }

    public IBattleSkill Get(WeaponSkillId id)
        => _skills.TryGetValue(id, out IBattleSkill skill) ? skill : _default;
}
