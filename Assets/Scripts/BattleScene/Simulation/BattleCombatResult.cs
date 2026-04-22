public readonly struct BattleCombatResult
{
    public BattleRuntimeUnit Attacker { get; }
    public BattleRuntimeUnit Target { get; }
    public float Damage { get; }
    public bool WasKill { get; }
    public bool WasSkill { get; }

    public BattleCombatResult(
        BattleRuntimeUnit attacker,
        BattleRuntimeUnit target,
        float damage,
        bool wasKill,
        bool wasSkill)
    {
        Attacker = attacker;
        Target = target;
        Damage = damage;
        WasKill = wasKill;
        WasSkill = wasSkill;
    }
}
