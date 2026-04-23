using System;
using UnityEngine;

public sealed class BattleSceneContext
{
    public BattleSimulationManager SimulationManager { get; }
    public SphereCollider BattlefieldCollider { get; }
    public BattleStatusGridUIManager StatusGridUI { get; }
    public BattleSceneUIManager SceneUI { get; }
    public BattleOrdersManager OrdersManager { get; }

    public BattleSceneContext(
        BattleSimulationManager simulationManager,
        SphereCollider battlefieldCollider,
        BattleStatusGridUIManager statusGridUI = null,
        BattleSceneUIManager sceneUI = null,
        BattleOrdersManager ordersManager = null
    )
    {
        SimulationManager = simulationManager ?? throw new ArgumentNullException(nameof(simulationManager));
        BattlefieldCollider = battlefieldCollider ?? throw new ArgumentNullException(nameof(battlefieldCollider));

        StatusGridUI = statusGridUI;
        SceneUI = sceneUI;
        OrdersManager = ordersManager;

        if (StatusGridUI == null)
            Debug.LogWarning("[BattleSceneContext] BattleStatusGridUIManager is null. UI 없이 실행 중.");
        if (SceneUI == null)
            Debug.LogWarning("[BattleSceneContext] BattleSceneUIManager is null. UI 없이 실행 중.");
        if (OrdersManager == null)
            Debug.LogWarning("[BattleSceneContext] BattleOrdersManager is null. LLM 주문 UI 없이 실행 중.");
    }
}
