using UnityEngine;

// 암살 교본: 적의 등 뒤에서 공격시 추가데미지. (1, 2, 3레벨)
public sealed class AssassinationManualArtifact : IDamageModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.AssassinationManual;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
    {
        if (request.Source != owner || request.Target == null)
            return;

        BattleUnitCombatState target = request.Target;

        if (target.CurrentTarget != null && target.CurrentTarget != owner)
        {
            Vector3 targetFacingDirection = (target.CurrentTarget.Position - target.Position).normalized;
            Vector3 attackDirection = (target.Position - owner.Position).normalized;

            if (Vector3.Dot(targetFacingDirection, attackDirection) > 0f)
            {
                request.Amount *= 1f + (_level * 0.15f);
            }
        }
    }
}
