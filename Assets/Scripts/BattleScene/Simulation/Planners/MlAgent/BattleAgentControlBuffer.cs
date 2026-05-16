using System.Collections.Generic;
using UnityEngine;

// BattleAgentControlBuffer는 ML-Agents action을 전투 시뮬레이션이 읽는 유닛별 제어 입력으로 보관한다.
// 입력 벡터 정규화, Agent 명령을 전투 명령으로 변환, 타겟 유효성 검사, fight mode 상태 반영,
// 실행된 일회성 명령 소비, observation용 입력 스냅샷 제공, 제어 해제 시 입력 초기화를 담당한다.
public sealed class BattleAgentControlBuffer
{
    private readonly Dictionary<BattleUnitCombatState, BattleAgentControlInput> _inputs =
        new Dictionary<BattleUnitCombatState, BattleAgentControlInput>();

    public void SetRawInput(
        BattleUnitCombatState self,
        Vector2 rawRelativeMove,
        GladiatorActionRole role,
        GladiatorFightMode fightMode,
        GladiatorAnchorKind anchorKind,
        int anchorSlot,
        GladiatorCommand command,
        BattleUnitCombatState target
    )
    {
        if (self == null)
        {
            return;
        }

        if (rawRelativeMove.sqrMagnitude > 1f)
        {
            rawRelativeMove.Normalize();
        }

        _inputs.TryGetValue(self, out BattleAgentControlInput input);
        input.PreviousRawLocalMove = input.RawLocalMove;
        input.RawLocalMove = rawRelativeMove;
        input.Role = role;
        input.FightMode = fightMode;
        input.AnchorKind = anchorKind;
        input.AnchorSlot = anchorSlot;
        input.Command = ToCommand(command);

        bool hasValidTarget = BattleFieldSnapshot.IsValidEnemyTarget(self, target);
        input.AnchorTarget = target;
        input.Target = hasValidTarget ? target : null;

        _inputs[self] = input;
        self.SetAgentFightMode(fightMode);
    }

    public BattleAgentControlInput GetInput(BattleUnitCombatState self)
    {
        if (self == null)
        {
            return default;
        }

        return _inputs.TryGetValue(self, out BattleAgentControlInput input) ? input : default;
    }

    public BattleAgentControlInput GetInputSnapshot(BattleUnitCombatState self)
    {
        return self != null && _inputs.TryGetValue(self, out BattleAgentControlInput input) ? input : default;
    }

    public void Clear(BattleUnitCombatState self)
    {
        if (self == null)
        {
            return;
        }

        _inputs.Remove(self);
        self.SetAgentFightMode(GladiatorFightMode.Neutral);
        self.SetPlannedTargets(null, null);
    }

    public void ClearAll()
    {
        _inputs.Clear();
    }

    private static BattleCombatCommand ToCommand(GladiatorCommand command)
    {
        switch (command)
        {
            case GladiatorCommand.Attack:
                return BattleCombatCommand.BasicAttack;
            default:
                return BattleCombatCommand.None;
        }
    }
}
