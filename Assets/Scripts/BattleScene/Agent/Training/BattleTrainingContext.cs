using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct BattleTrainingContext
{
    public readonly BattleUnitCombatState Self;
    public readonly IReadOnlyList<BattleUnitCombatState> Teammates;
    public readonly IReadOnlyList<BattleUnitCombatState> Opponents;
    public readonly Vector3 ArenaCenter;
    public readonly float ArenaRadius;

    public BattleTrainingContext(
        BattleUnitCombatState self,
        IReadOnlyList<BattleUnitCombatState> teammates,
        IReadOnlyList<BattleUnitCombatState> opponents,
        Vector3 arenaCenter,
        float arenaRadius
    )
    {
        Self = self;
        Teammates = teammates ?? Array.Empty<BattleUnitCombatState>();
        Opponents = opponents ?? Array.Empty<BattleUnitCombatState>();
        ArenaCenter = arenaCenter;
        ArenaRadius = arenaRadius;
    }
}
