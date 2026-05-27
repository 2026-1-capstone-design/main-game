using System.Collections.Generic;
using UnityEngine;

public sealed class OblivionSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Oblivion;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.dagger };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 15f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        if (caster == null)
            return;

        foreach (var unit in context.Units)
        {
            if (
                BattleFieldSnapshot.IsValidEnemyTarget(caster.State, unit?.State)
                && Vector3.Distance(caster.Position, unit.Position) <= AreaRadius
            )
            {
                effects.GrantTemporaryArtifact(unit, new OblivionArtifact(), 10f, context);

                GameObject activeVfx = VFXManager.Instance.PlayEffect("DarkHit", unit.Position + Vector3.up);
            }
        }
    }

    private class OblivionArtifact : IDamageModifierArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;
        private int _blockCount = 3;

        public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context) { }

        public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
        {
            if (request.Source == owner && _blockCount > 0)
            {
                request.Amount = 0f;
                _blockCount--;
            }
        }
    }
}
