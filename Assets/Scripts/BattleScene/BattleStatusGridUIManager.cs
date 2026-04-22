using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// BattleStatusGridUIManager 책임 (전투 디버그 UI 전담 manager):
// - 하단 2×6 status panel 갱신 (ally 1~6 / enemy 7~12 상태 텍스트 표시)
// - 상단 speed text 표시
// 각 셀에 표시: 유닛 번호, 이름, HP, 현재 행동, 이동/공격 여부, 현재 타겟,
//              KeepBehaving, ActionTimer, 최고 점수 행동/값, RAW parameter set, MOD parameter set
[DisallowMultipleComponent]
public sealed class BattleStatusGridUIManager : MonoBehaviour
{
    [Header("Top")]
    [SerializeField] private TMP_Text simulationSpeedText;  // 현재 배속 텍스트

    [Header("Ally Cells (1~6)")]
    [SerializeField] private TMP_Text[] allyStatusTexts = new TMP_Text[6];
    [SerializeField] private Button[] allyOrderButtons = new Button[6];

    [Header("Enemy Cells (7~12)")]
    [SerializeField] private TMP_Text[] enemyStatusTexts = new TMP_Text[6];

    private BattleSimulationManager _simulationManager;
    private BattleSceneUIManager _battleSceneUIManager;
    private readonly BattleRuntimeUnit[] _allyUnits = new BattleRuntimeUnit[6];
    private readonly BattleRuntimeUnit[] _enemyUnits = new BattleRuntimeUnit[6];
    private bool _initialized;

    public void Initialize(
        BattleSimulationManager simulationManager,
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits,
        BattleSceneUIManager battleSceneUIManager = null)
    {
        _simulationManager = simulationManager;
        _battleSceneUIManager = battleSceneUIManager;
        BindUnits(runtimeUnits);

        BindAllyOrderButtons();
        UpdateAllyOrderButtonInteractableStates();
        _initialized = true;
    }

    public void BindUnits(IReadOnlyList<BattleRuntimeUnit> runtimeUnits)
    {
        for (int i = 0; i < _allyUnits.Length; i++)
        {
            _allyUnits[i] = null;
        }

        for (int i = 0; i < _enemyUnits.Length; i++)
        {
            _enemyUnits[i] = null;
        }

        if (runtimeUnits != null)
        {
            for (int i = 0; i < runtimeUnits.Count; i++)
            {
                BattleRuntimeUnit unit = runtimeUnits[i];
                if (unit == null)
                {
                    continue;
                }

                if (!unit.IsEnemy)
                {
                    int allyIndex = unit.UnitNumber - 1;
                    if (allyIndex >= 0 && allyIndex < _allyUnits.Length)
                    {
                        _allyUnits[allyIndex] = unit;
                    }
                }
                else
                {
                    int enemyIndex = unit.UnitNumber - 7;
                    if (enemyIndex >= 0 && enemyIndex < _enemyUnits.Length)
                    {
                        _enemyUnits[enemyIndex] = unit;
                    }
                }
            }
        }

        UpdateAllyOrderButtonInteractableStates();
    }

    public void Refresh()
    {
        if (!_initialized)
        {
            return;
        }

        if (simulationSpeedText != null)
        {
            float speed = _simulationManager != null ? _simulationManager.SimulationSpeedMultiplier : 0f;
            simulationSpeedText.text = $"Speed x{speed:0.##}";
        }

        UpdateAllyOrderButtonInteractableStates();

        for (int i = 0; i < allyStatusTexts.Length; i++)
        {
            if (allyStatusTexts[i] != null)
            {
                allyStatusTexts[i].text = FormatUnit(_allyUnits[i]);
            }
        }

        for (int i = 0; i < enemyStatusTexts.Length; i++)
        {
            if (enemyStatusTexts[i] != null)
            {
                enemyStatusTexts[i].text = FormatUnit(_enemyUnits[i]);
            }
        }
    }

    private void BindAllyOrderButtons()
    {
        for (int i = 0; i < allyOrderButtons.Length; i++)
        {
            Button button = allyOrderButtons[i];
            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();

            int capturedIndex = i;
            button.onClick.AddListener(() => OnAllyOrderButtonClicked(capturedIndex));
        }
    }

    private void UpdateAllyOrderButtonInteractableStates()
    {
        for (int i = 0; i < allyOrderButtons.Length; i++)
        {
            Button button = allyOrderButtons[i];
            if (button == null)
            {
                continue;
            }

            BattleRuntimeUnit targetUnit = i >= 0 && i < _allyUnits.Length ? _allyUnits[i] : null;
            button.interactable = _battleSceneUIManager != null && targetUnit != null && !targetUnit.IsCombatDisabled;
        }
    }

    private void OnAllyOrderButtonClicked(int allyIndex)
    {
        if (_battleSceneUIManager == null)
        {
            Debug.LogWarning("[BattleStatusGridUIManager] Ally order click ignored. BattleSceneUIManager is null.", this);
            return;
        }

        if (allyIndex < 0 || allyIndex >= _allyUnits.Length)
        {
            Debug.LogWarning($"[BattleStatusGridUIManager] Ally order click ignored. Invalid ally index={allyIndex}", this);
            return;
        }

        BattleRuntimeUnit targetUnit = _allyUnits[allyIndex];
        if (targetUnit == null)
        {
            Debug.LogWarning($"[BattleStatusGridUIManager] Ally order click ignored. Ally slot {allyIndex + 1} is empty.", this);
            return;
        }

        if (targetUnit.IsCombatDisabled)
        {
            Debug.LogWarning($"[BattleStatusGridUIManager] Ally order click ignored. Ally slot {allyIndex + 1} is disabled.", this);
            return;
        }

        _battleSceneUIManager.OpenSingleOrderPanel(targetUnit);
    }

    private string FormatUnit(BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return "Empty";
        }

        WeaponType weaponType = WeaponType.None;
        if (unit.Snapshot != null)
        {
            weaponType = unit.Snapshot.WeaponType;
        }

        string hpText = $"HP {unit.CurrentHealth:0.#}/{unit.MaxHealth:0.#}";
        string moveText = unit.IsMoving ? "Y" : "N";
        string attackText = unit.IsAttacking ? "Y" : "N";
        string targetText = unit.CurrentTarget != null ? unit.CurrentTarget.UnitNumber.ToString() : "-";
        string topActionText = unit.TopScoredAction == BattleActionType.None ? "-" : unit.TopScoredAction.ToString();

        return
            $"#{unit.UnitNumber} {unit.DisplayName} {weaponType}\n" +
            $"{hpText}\n" +
            $"Action {unit.CurrentAction}\n" +
            $"Move {moveText} / Attack {attackText}  {targetText}\n" +
            $"Keep {unit.KeepBehaving:0.#} / T {unit.ActionTimer:0.#}\n" +
            $"Top {topActionText} {unit.TopScoredValue:0.##}\n" +
            $"R {FormatScoresCompact(unit.CurrentScores)}\n" +
            $"RAW {FormatParametersCompact(unit.CurrentRawParameters)}\n" +
            $"MOD {FormatParametersCompact(unit.CurrentModifiedParameters)}";
    }

    private string FormatParametersCompact(BattleParameterSet p)
    {
        return
            $"h{p.SelfHpLow:0.0} " +
            $"s{p.SelfSurroundedByEnemies:0.0} " +
            $"la{p.LowHealthAllyProximity:0.0} " +
            $"fp{p.AllyUnderFocusPressure:0.0} " +
            $"g{p.AllyFrontlineGap:0.0} " +
            $"i{p.IsolatedEnemyVulnerability:0.0} " +
            $"c{p.EnemyClusterDensity:0.0} " +
            $"tc{p.DistanceToTeamCenter:0.0} " +
            $"atk{p.SelfCanAttackNow:0.0}";
    }
    private string FormatScoresCompact(BattleActionScoreSet s)
    {
        return
            $"as{s.AssassinateIsolatedEnemy:0.0} " +
            $"dv{s.DiveEnemyBackline:0.0} " +
            $"pe{s.PeelForWeakAlly:0.0} " +
            $"es{s.EscapeFromPressure:0.0} " +
            $"rg{s.RegroupToAllies:0.0} " +
            $"cl{s.CollapseOnCluster:0.0} " +
            $"en{s.EngageNearest:0.0}";
    }
}
