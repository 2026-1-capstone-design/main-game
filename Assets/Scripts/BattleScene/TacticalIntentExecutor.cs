// ============================================================
//  TacticalIntentExecutor.cs
//  Assets/Scripts/BattleScene/ 에 넣으세요.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticalIntentExecutor : MonoBehaviour
{
    // ── 내부 상태 ────────────────────────────────────────
    private readonly Dictionary<int, TacticalIntent> _activeIntents
        = new Dictionary<int, TacticalIntent>();

    private BattleSimulationManager _sim;
    private bool _initialized;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    // ── 1단계 테스트용: Inspector에서 하드코딩 의도 주입 ──
    [System.Serializable]
    private struct DebugIntentEntry
    {
        [Tooltip("아군 유닛 번호 (1~6)")]
        public int allyUnitNumber;

        public TacticalIntentType intent;
        public SkillUsagePolicy skillPolicy;
        public PositioningStyle positioning;

        [Tooltip("주 타겟 적 유닛 번호 (7~12). 0이면 자동 선택")]
        public int primaryTargetEnemyNumber;
    }

    [Header("─── 1단계 Debug: 하드코딩 의도 주입 ───")]
    [Tooltip("Play 시작 후 이 목록대로 각 유닛에 의도를 강제 주입합니다.")]
    [SerializeField] private List<DebugIntentEntry> debugIntents = new List<DebugIntentEntry>();

    // ─────────────────────────────────────────────────────

    public void Initialize(BattleSimulationManager sim)
    {
        _sim = sim;
        _activeIntents.Clear();
        _initialized = true;

#if UNITY_EDITOR
        ApplyDebugIntents();
#endif
    }

    public void SetIntent(BattleRuntimeUnit unit, TacticalIntent intent)
    {
        if (unit == null || intent == null)
            return;

        _activeIntents[unit.UnitNumber] = intent;

        if (verboseLog)
            Debug.Log($"[IntentExecutor] Intent set → Unit={unit.DisplayName}({unit.UnitNumber}), {intent}");
    }

    public void ClearIntent(BattleRuntimeUnit unit)
    {
        if (unit == null)
            return;
        _activeIntents.Remove(unit.UnitNumber);
    }

    public void TickIntents()
    {
        if (!_initialized || _sim == null)
            return;

        IReadOnlyList<BattleRuntimeUnit> units = _sim.RuntimeUnits;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled || unit.IsEnemy)
                continue;

            if (!_activeIntents.TryGetValue(unit.UnitNumber, out TacticalIntent intent))
                continue;

            if (intent.NeedsReEvaluation(unit))
            {
                if (verboseLog)
                    Debug.Log($"[IntentExecutor] Intent expired → Unit={unit.DisplayName}, Reason: target dead or self dead");

                _activeIntents.Remove(unit.UnitNumber);
                unit.ClearIntentOverride();
                continue;
            }

            ApplyIntentToUnit(unit, intent);
        }
    }

    private void ApplyIntentToUnit(BattleRuntimeUnit unit, TacticalIntent intent)
    {
        BattleActionType targetActionType = TranslateIntent(intent);
        BattleRuntimeUnit targetUnit = intent.Intent == TacticalIntentType.Support
            ? ResolveSupportAllyTarget(unit, intent)
            : ResolveTarget(unit, intent);

        unit.SetIntentOverride(targetActionType, targetUnit, intent.SkillPolicy, intent.Positioning);
    }

    private BattleActionType TranslateIntent(TacticalIntent intent)
    {
        switch (intent.Intent)
        {
            case TacticalIntentType.Assassinate:
                return intent.PrimaryTarget != null
                    ? BattleActionType.AssassinateIsolatedEnemy
                    : BattleActionType.EngageNearest;

            case TacticalIntentType.Support:
                return BattleActionType.PeelForWeakAlly;

            case TacticalIntentType.Vanguard:
                return BattleActionType.CollapseOnCluster;

            case TacticalIntentType.HoldLine:
                return BattleActionType.EngageNearest;

            case TacticalIntentType.Retreat:
                return BattleActionType.EscapeFromPressure;

            // KITE는 공격 가능한 상태를 유지하면서 사거리 중심으로 이동을 조정한다.
            case TacticalIntentType.Kite:
                return BattleActionType.EngageNearest;

            // REGROUP은 팀 중심점으로 복귀하는 전술이므로 전용 런타임 액션으로 보낸다.
            case TacticalIntentType.Regroup:
                return BattleActionType.RegroupToAllies;

            default:
                return BattleActionType.EngageNearest;
        }
    }

    private BattleRuntimeUnit ResolveTarget(BattleRuntimeUnit unit, TacticalIntent intent)
    {
        if (intent.Intent == TacticalIntentType.Retreat)
            return null;

        // REGROUP은 특정 적을 타격하지 않으므로 타겟을 두지 않는다.
        if (intent.Intent == TacticalIntentType.Regroup)
            return null;

        if (intent.PrimaryTarget != null && !intent.PrimaryTarget.IsCombatDisabled)
            return intent.PrimaryTarget;

        return FindNearestEnemy(unit);
    }

    private BattleRuntimeUnit ResolveSupportAllyTarget(BattleRuntimeUnit unit, TacticalIntent intent)
    {
        BattleRuntimeUnit secondary = intent.SecondaryTarget;
        if (secondary != null && !secondary.IsEnemy && !secondary.IsCombatDisabled)
            return secondary;

        if (_sim == null)
            return null;

        IReadOnlyList<BattleRuntimeUnit> units = _sim.RuntimeUnits;
        BattleRuntimeUnit nearest = null;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit ally = units[i];
            if (ally == null || ally == unit || ally.IsCombatDisabled || ally.IsEnemy)
                continue;

            Vector3 delta = ally.Position - unit.Position;
            delta.y = 0f;
            float distSqr = delta.sqrMagnitude;

            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                nearest = ally;
            }
        }

        return nearest;
    }

    private BattleRuntimeUnit FindNearestEnemy(BattleRuntimeUnit requester)
    {
        if (_sim == null)
            return null;

        IReadOnlyList<BattleRuntimeUnit> units = _sim.RuntimeUnits;
        BattleRuntimeUnit nearest = null;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit candidate = units[i];
            if (candidate == null || candidate.IsCombatDisabled)
                continue;
            if (candidate.IsEnemy == requester.IsEnemy)
                continue;

            Vector3 delta = candidate.Position - requester.Position;
            delta.y = 0f;
            float distSqr = delta.sqrMagnitude;

            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                nearest = candidate;
            }
        }
        return nearest;
    }

#if UNITY_EDITOR
    private void ApplyDebugIntents()
    {
        if (debugIntents == null || debugIntents.Count == 0)
            return;

        IReadOnlyList<BattleRuntimeUnit> units = _sim.RuntimeUnits;

        foreach (DebugIntentEntry entry in debugIntents)
        {
            BattleRuntimeUnit targetUnit = null;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null && units[i].UnitNumber == entry.allyUnitNumber && !units[i].IsEnemy)
                {
                    targetUnit = units[i];
                    break;
                }
            }

            if (targetUnit == null)
            {
                Debug.LogWarning($"[IntentExecutor] Debug: Ally unit #{entry.allyUnitNumber} not found.");
                continue;
            }

            BattleRuntimeUnit primaryTarget = null;
            if (entry.primaryTargetEnemyNumber > 0)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    if (units[i] != null && units[i].UnitNumber == entry.primaryTargetEnemyNumber && units[i].IsEnemy)
                    {
                        primaryTarget = units[i];
                        break;
                    }
                }
            }

            TacticalIntent intent = new TacticalIntent(
                entry.intent,
                entry.skillPolicy,
                entry.positioning,
                primaryTarget
            );

            SetIntent(targetUnit, intent);
        }
    }

    [ContextMenu("Reapply Debug Intents")]
    private void ReapplyDebugIntents()
    {
        if (!Application.isPlaying || !_initialized)
            return;
        _activeIntents.Clear();
        ApplyDebugIntents();
        Debug.Log("[IntentExecutor] Debug intents reapplied.");
    }
#endif
}
