# 006. BattleSceneFlowManager 암묵적 초기화 순서 명시화

## 문제 상황

`BattleSceneFlowManager`는 전투 씬의 모든 매니저를 조율하는 진입점이다. 그런데 네 매니저의 초기화 순서가 코드에 명시적으로 드러나지 않고, 호출 순서를 어기면 null 참조 또는 잘못된 상태로 이어지는 암묵적 의존 관계가 존재한다.

### 현재 초기화 흐름의 문제

```csharp
// BattleSceneFlowManager.BootstrapFromPayload() — 단순화한 구조
void BootstrapFromPayload(BattleStartPayload payload)
{
    // 1. 유닛 스폰
    SpawnUnits(payload);               // BattleRuntimeUnit[] 생성

    // 2. 시뮬레이션 초기화 — statusGridUIManager가 null이어도 계속 진행
    _simulationManager.Initialize(
        _runtimeUnits,
        _battlefieldCollider,
        _statusGridUIManager,          // ← null 가능
        _battleSceneUIManager,         // ← null 가능
        payload);

    // 3. UI 초기화 — 시뮬레이션 이후 실행되어야 함
    _statusGridUIManager?.BindUnits(_runtimeUnits);
    _ordersManager?.Initialize(_runtimeUnits);
}
```

**문제 1 — 조건부 초기화로 인한 무음 실패**  
`_statusGridUIManager`가 null이면 `Initialize()` 호출은 성공하지만 UI 갱신이 영원히 일어나지 않는다. 어떤 UI가 누락됐는지 런타임 로그가 없다.

**문제 2 — 순서 의존이 코드가 아닌 호출 위치에 인코딩됨**  
`SpawnUnits()` 이전에 `_simulationManager.Initialize()`를 호출하면 유닛 배열이 비어 있다. 이 순서는 메서드 시그니처나 주석에 표현되지 않는다. 새 기능 추가 시 순서를 깨뜨리기 쉽다.

**문제 3 — TrainingBootstrapper의 부분 우회**  
```csharp
// TrainingBootstrapper.cs 일부
// BattleSessionManager를 완전히 우회하여 직접 payload 구성
battleSceneFlowManager.autoBootstrapFromSessionManager = false;
battleSceneFlowManager.BootstrapFromPayload(payload);
```
`TrainingBootstrapper`는 `BattleSessionManager` 없이 `BootstrapFromPayload`를 호출한다. 이 경로에서 `_statusGridUIManager`·`_ordersManager`가 씬에 없을 때 어떻게 동작해야 하는지 정책이 명시되어 있지 않다.

**문제 4 — F7 인플레이스 리스타트**  
```csharp
void RestartCurrentBattle()  // F7
{
    DestroyAllUnits();
    BootstrapFromPayload(_clonedPayload);  // 초기화 전체 재실행
}
```
`_clonedPayload`가 null이면 (`BootstrapFromPayload`가 아직 한 번도 완료되지 않은 상태) `RestartCurrentBattle()`은 null 역참조가 발생한다. null 가드가 없다.

---

## 해결 방안

### 단계 1 — BattleSceneContext로 의존성 명시

```csharp
// 신규: BattleSceneContext.cs
public sealed class BattleSceneContext
{
    public readonly BattleSimulationManager  SimulationManager;   // 필수
    public readonly BoxCollider              BattlefieldCollider; // 필수
    public readonly BattleStatusGridUIManager StatusGridUI;       // 선택
    public readonly BattleSceneUIManager     SceneUI;             // 선택
    public readonly BattleOrdersManager      OrdersManager;       // 선택

    // 필수 항목 누락 시 생성자에서 ArgumentNullException
    public BattleSceneContext(
        BattleSimulationManager sim,
        BoxCollider collider,
        BattleStatusGridUIManager statusGrid = null,
        BattleSceneUIManager sceneUI = null,
        BattleOrdersManager orders = null)
    {
        SimulationManager   = sim      ?? throw new ArgumentNullException(nameof(sim));
        BattlefieldCollider = collider ?? throw new ArgumentNullException(nameof(collider));
        StatusGridUI  = statusGrid;
        SceneUI       = sceneUI;
        OrdersManager = orders;
    }
}
```

### 단계 2 — BattleBootstrapper로 초기화 단계 명시

```csharp
// 신규: BattleBootstrapper.cs
public static class BattleBootstrapper
{
    // 반환형이 명시적 단계 결과라 순서 역전이 컴파일 타임 오류가 됨
    public static SpawnResult  SpawnUnits(BattleStartPayload payload, Transform[] slots);
    public static void         InitializeSimulation(SpawnResult spawned, BattleSceneContext ctx, BattleStartPayload payload);
    public static void         InitializeUI(SpawnResult spawned, BattleSceneContext ctx);

    // 세 단계를 순서대로 실행하는 편의 메서드
    public static SpawnResult Bootstrap(
        BattleStartPayload payload,
        Transform[] slots,
        BattleSceneContext ctx)
    {
        var spawned = SpawnUnits(payload, slots);
        InitializeSimulation(spawned, ctx, payload);
        InitializeUI(spawned, ctx);
        return spawned;
    }
}
```

`BattleSceneFlowManager`는 내부적으로 `BattleBootstrapper.Bootstrap()`을 호출하는 얇은 MonoBehaviour 진입점이 된다.

### 단계 3 — null 가드 및 상태 검증 추가

```csharp
public void RestartCurrentBattle()
{
    if (_clonedPayload == null)
    {
        Debug.LogError("[FlowManager] RestartCurrentBattle() called before initial Bootstrap.", this);
        return;
    }
    DestroyAllUnits();
    Bootstrap(_clonedPayload);
}
```

---

## 기대 효과

| 항목 | 현재 | 개선 후 |
|---|---|---|
| 초기화 순서 파악 | `BootstrapFromPayload()` 전체 읽어야 파악 | `BattleBootstrapper` 세 메서드 시그니처로 명시 |
| 필수 의존성 누락 | null 역참조가 런타임에 발생 | 생성자에서 즉시 예외 |
| TrainingBootstrapper 우회 경로 | 무문서 관행 | `BattleSceneContext`에서 선택 항목 명시 |
| F7 리스타트 null 버그 | 잠재 크래시 | null 가드 + 명확한 오류 메시지 |
| 초기화 로직 단위 테스트 | `BattleSceneFlowManager` 전체 씬 필요 | `BattleBootstrapper` 정적 메서드로 분리 테스트 |

---

## 영향 범위

### 수정 대상 파일
- `Assets/Scripts/BattleScene/BattleSceneFlowManager.cs`  
  — `BootstrapFromPayload()` 내부를 `BattleBootstrapper.Bootstrap()`으로 교체  
  — `RestartCurrentBattle()`에 null 가드 추가  
  — `BattleSceneContext` 구성 및 캐싱 로직 추가

### 신규 생성 파일
```
Assets/Scripts/BattleScene/
  BattleSceneContext.cs
  BattleBootstrapper.cs
  SpawnResult.cs          ← 스폰 결과 DTO (유닛 목록 + 슬롯 매핑)
```

### 영향받는 씬 / 에셋
- `BattleScene.unity` 및 `TrainingScene.unity`  
  — `BattleSceneFlowManager` Inspector 참조 유지, 변경 없음
- `TrainingBootstrapper.cs`  
  — `BootstrapFromPayload()` 직접 호출 방식은 유지 가능, `BattleSceneContext` 구성만 추가

### 위험도
**낮음.** `BattleSceneFlowManager`의 public API(`BootstrapFromPayload`, `RestartCurrentBattle`)는 시그니처가 바뀌지 않는다. 내부 로직이 `BattleBootstrapper`로 이동할 뿐이다. 기존 씬 설정은 수정 없이 유지된다.

---

## 구현 단계

### Step 1 — SpawnResult DTO 생성
`Assets/Scripts/BattleScene/SpawnResult.cs`를 생성한다. 스폰 결과로 생성된 `BattleRuntimeUnit` 목록과 UnitNumber → 유닛 매핑 딕셔너리를 담는다.

```csharp
public sealed class SpawnResult
{
    public readonly IReadOnlyList<BattleRuntimeUnit> Units;
    public readonly IReadOnlyDictionary<int, BattleRuntimeUnit> ByUnitNumber;

    public SpawnResult(List<BattleRuntimeUnit> units)
    {
        Units = units.AsReadOnly();
        var dict = new Dictionary<int, BattleRuntimeUnit>(units.Count);
        foreach (var u in units) dict[u.UnitNumber] = u;
        ByUnitNumber = dict;
    }
}
```

### Step 2 — BattleSceneContext 생성
`Assets/Scripts/BattleScene/BattleSceneContext.cs`를 생성한다. 생성자에서 필수 파라미터(`SimulationManager`, `BattlefieldCollider`)에 null이면 `ArgumentNullException`을 던지고, 선택 파라미터는 null을 허용한다. 선택 파라미터가 null일 때 Debug.Log로 "UI 없이 실행 중" 메시지를 출력한다.

### Step 3 — BattleBootstrapper 정적 클래스 구현
`Assets/Scripts/BattleScene/BattleBootstrapper.cs`를 생성한다. `BattleSceneFlowManager.BootstrapFromPayload()`의 내부 로직을 세 메서드로 분리한다.

- `SpawnUnits()`: 유닛 프리팹 인스턴스화, 스냅샷 적용, `SpawnResult` 반환
- `InitializeSimulation()`: `ctx.SimulationManager.Initialize()` 호출
- `InitializeUI()`: `ctx.StatusGridUI?.BindUnits()`, `ctx.OrdersManager?.Initialize()` 호출 및 null 로깅

각 메서드는 독립적으로 호출 가능하지만, `Bootstrap()` 편의 메서드가 순서를 보장한다.

### Step 4 — BattleSceneFlowManager 내부 교체
`BootstrapFromPayload()` 내부를 아래 형태로 교체한다.

```csharp
public void BootstrapFromPayload(BattleStartPayload payload)
{
    _clonedPayload = payload.Clone();
    var ctx = new BattleSceneContext(
        _simulationManager,
        _battlefieldCollider,
        _statusGridUIManager,
        _battleSceneUIManager,
        _ordersManager);
    _spawnResult = BattleBootstrapper.Bootstrap(_clonedPayload, _unitSlots, ctx);
}
```

`_unitSlots`는 기존에 FlowManager가 보유하던 슬롯 Transform 배열이다.

### Step 5 — RestartCurrentBattle null 가드 추가
```csharp
public void RestartCurrentBattle()
{
    if (_clonedPayload == null)
    {
        Debug.LogError("[FlowManager] RestartCurrentBattle() called before initial Bootstrap.", this);
        return;
    }
    DestroyAllUnits();
    BootstrapFromPayload(_clonedPayload);
}
```

### Step 6 — TrainingBootstrapper 업데이트
`TrainingBootstrapper`가 `BootstrapFromPayload()`를 직접 호출하는 부분은 그대로 유지된다. 단, `BattleSceneContext` 없이 `BattleBootstrapper`의 개별 메서드를 직접 호출하는 경로가 필요하다면 `BattleBootstrapper.SpawnUnits()`만 별도 호출하는 방식으로 수정한다.

### Step 7 — 검증
- BattleScene에서 전투 시작 → F7 리스타트 → 정상 동작 확인
- `_statusGridUIManager`를 Inspector에서 null로 설정한 채 실행 → null 경고 메시지 출력 확인
- TrainingScene에서 에피소드 재시작 10회 반복 → null 역참조 없음 확인
