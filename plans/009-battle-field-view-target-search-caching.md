# 009. BattleFieldView O(n²) 타겟 탐색 캐싱

## 문제 상황

`BattleFieldView`는 전투 유닛 목록을 래핑하고 플래너(Planner)와 스킬에게 타겟 탐색 서비스를 제공한다. 문제는 **모든 탐색 결과가 캐시 없이 매 틱마다 재계산**된다는 것이다.

### O(n²) 중복 연산의 실제 규모

```
시뮬레이션 한 틱에서 발생하는 연산 흐름:

BattleSimulationManager.StepSimulation()
  └─ BuildAllExecutionPlans()
       └─ foreach unit (12회):
            └─ planner.Build(unit, _fieldView)
                 ├─ AssassinatePlanner  → FindBestIsolatedEnemy()   ← enemy 전체 순회
                 ├─ EscapePlanner       → ComputeEnemyPressureCenter() ← enemy 전체 가중합
                 ├─ PeelForWeakAlly     → FindMostPressuredAlly()   ← ally + enemy 이중 순회
                 └─ SupportWeakAlly    → FindMostPressuredAlly()   ← 동일 연산 재실행
```

`FindBestIsolatedEnemy()`는 적군 6명에 대해 각각 아군 6명과의 거리를 계산하므로 호출당 O(n×m)이다. 이것이 12유닛 × 최대 7플래너 = 매 틱마다 최대 **84번** 호출될 수 있다. 15 tick/sec 기준 초당 1,260번의 적군 격리 점수 재계산이 발생한다.

### 캐시 없는 BattleUnitView 재생성

```csharp
// BattleFieldView 내부 — FindBestIsolatedEnemy 일부
List<BattleUnitView> enemies = GetLivingEnemyViews(requestingUnit);  // 매번 새 List 생성
for (int i = 0; i < enemies.Count; i++)
{
    float score = BattleParameterComputer.ComputeIsolatedEnemyTargetScore(...);
    // 내부에서 또 ally 목록을 순회
}
```

`GetLivingEnemyViews()`는 호출마다 새 `List<BattleUnitView>`를 생성한다. `BattleUnitView`는 struct지만 List boxing은 GC 압력을 만든다. 훈련 환경에서 배속(×8)으로 실행되면 이 비용이 8배로 증폭된다.

### 플래너 간 타겟 불일치 가능성

현재 구조에서는 `AssassinatePlanner`와 `SupportWeakAlly`가 같은 틱 안에서 서로 다른 시점의 유닛 상태를 읽을 수 있다. `ExecuteAttackPhase`가 중간에 끼어들어 유닛을 전투 불능으로 만들면, 이후 플래너가 이미 죽은 타겟을 대상으로 계획을 세운다. `IsUsable()`이 재검증하지만, 이 문제가 없었다면 불필요한 계획 폐기를 줄일 수 있다.

---

## 해결 방안

### 단계 1 — TickSnapshot 도입

시뮬레이션 틱 시작 시점에 `BattleFieldSnapshot`을 한 번 생성하고, 해당 틱의 모든 탐색이 이 스냅샷을 사용하도록 한다.

```csharp
// 신규: BattleFieldSnapshot.cs
public sealed class BattleFieldSnapshot
{
    // 틱 시작 시 한 번 계산, 불변
    public IReadOnlyList<BattleUnitView> AllLiving        { get; }
    public IReadOnlyList<BattleUnitView> LivingAllies     { get; }
    public IReadOnlyList<BattleUnitView> LivingEnemies    { get; }

    // 비용 높은 탐색 결과 — 틱당 1회만 계산
    public BattleUnitView? BestIsolatedEnemy     { get; }
    public BattleUnitView? MostPressuredAlly     { get; }
    public BattleUnitView? BestBacklineEnemy     { get; }
    public Vector3          EnemyPressureCenter   { get; }
    public Vector3          AllyTeamCenter        { get; }

    public static BattleFieldSnapshot Build(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleParameterRadii radii);
}
```

### 단계 2 — IBattleActionPlanner 시그니처 업데이트

```csharp
// 현재
BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldView field);

// 변경 후
BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldSnapshot snapshot);
```

플래너는 `snapshot.BestIsolatedEnemy`처럼 이미 계산된 결과를 참조한다. 탐색 로직은 `BattleFieldSnapshot.Build()` 안으로 이동한다.

### 단계 3 — 재사용 가능한 버퍼 사용

```csharp
// BattleFieldSnapshot.Build() 내부
private static readonly List<BattleUnitView> _allyBuffer  = new(6);
private static readonly List<BattleUnitView> _enemyBuffer = new(6);
```

정적 버퍼를 재사용해 List 할당을 제거한다. `Build()`는 `BattleSimulationManager`의 `BuildAllExecutionPlans()` 진입 시 호출되어 GC를 최소화한다.

---

## 기대 효과

| 항목 | 현재 | 개선 후 |
|---|---|---|
| `FindBestIsolatedEnemy()` 호출 횟수 | 틱당 최대 84회 | 틱당 1회 |
| `List<BattleUnitView>` 할당 횟수 | 탐색 호출당 N회 | 0회 (정적 버퍼 재사용) |
| 플래너 간 타겟 불일치 | 실행 중 유닛 상태 변화로 가능 | 틱 시작 스냅샷 기준으로 일관성 보장 |
| 탐색 로직 테스트 | `BattleFieldView` 전체 mock 필요 | `BattleFieldSnapshot.Build()` 단독 테스트 가능 |
| 배속 ×8 훈련 성능 | O(n²) × 8배 GC 압력 | 할당 최소화로 훈련 처리량 개선 |

---

## 영향 범위

### 수정 대상 파일
- `Assets/Scripts/BattleScene/BattleFieldView.cs`  
  — 탐색 메서드들을 `BattleFieldSnapshot.Build()`로 이동, `BattleFieldView`는 런타임 유닛 접근자만 유지하거나 제거 검토
- `Assets/Scripts/BattleScene/BattleSimulationManager.cs`  
  — `BuildAllExecutionPlans()` 시작부에 `BattleFieldSnapshot.Build()` 호출 추가
- 모든 `IBattleActionPlanner` 구현체  
  — `Build(unit, field)` → `Build(unit, snapshot)` 시그니처 변경  
  — 대상: `AssassinatePlanner`, `EscapePlanner`, `RegroupPlanner`, `PeelForWeakAllyPlanner`, `SupportWeakAllyPlanner`, `EngageNearestPlanner`, `IdlePlanner`

### 신규 생성 파일
```
Assets/Scripts/BattleScene/
  BattleFieldSnapshot.cs
```

### 영향받는 인터페이스
- `IBattleActionPlanner` 시그니처 변경 → 구현체 7개 전부 수정 필요
- `IBattleSkill` 이 `BattleFieldView`를 참조하는지 확인 후 동일 패턴 적용 검토

### 위험도
**낮음.** 탐색 로직의 이동이지 재작성이 아니다. `BattleFieldSnapshot`의 계산 결과가 기존 `BattleFieldView`의 결과와 동일한지 기존 동작으로 회귀 테스트하기 용이하다. 인터페이스 시그니처 변경으로 컴파일 타임에 누락을 감지할 수 있다.

---

## 구현 단계

### Step 1 — IBattleSkill의 BattleFieldView 참조 여부 확인
`IBattleSkill` 및 구현체들이 `BattleFieldView`를 직접 사용하는지 Grep으로 확인한다. 사용한다면 Step 2에서 `BattleFieldSnapshot`으로 함께 전환 대상에 포함시킨다.

```
검색: BattleFieldView (Assets/Scripts 전체)
```

### Step 2 — BattleFieldSnapshot.cs 생성 (정적 버퍼 포함)
`Assets/Scripts/BattleScene/BattleFieldSnapshot.cs`를 생성한다. 클래스 상단에 정적 재사용 버퍼를 선언한다.

```csharp
private static readonly List<BattleUnitView> _allyBuf  = new(6);
private static readonly List<BattleUnitView> _enemyBuf = new(6);
```

`Build()` 내부에서 버퍼를 Clear 후 채우고, `AllLiving`, `LivingAllies`, `LivingEnemies`를 `Array.AsReadOnly()` 또는 `List.AsReadOnly()`로 노출한다.

### Step 3 — 탐색 메서드를 BattleFieldView에서 BattleFieldSnapshot으로 이동
`BattleFieldView`의 탐색 메서드들을 하나씩 `BattleFieldSnapshot.Build()` 내부로 이동한다. 이동 순서:

1. `FindBestIsolatedEnemy()` → `BestIsolatedEnemy` 프로퍼티
2. `FindMostPressuredAlly()` → `MostPressuredAlly` 프로퍼티
3. `FindBestBacklineEnemy()` → `BestBacklineEnemy` 프로퍼티
4. `ComputeEnemyPressureCenter()` → `EnemyPressureCenter` 프로퍼티
5. `ComputeAllyTeamCenter()` → `AllyTeamCenter` 프로퍼티

메서드를 하나 이동할 때마다 컴파일하여 누락을 즉시 확인한다.

### Step 4 — IBattleActionPlanner 시그니처 변경
`IBattleActionPlanner.cs`의 `Build` 시그니처를 변경한다.

```csharp
// 변경 전
BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldView field);

// 변경 후
BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldSnapshot snapshot);
```

컴파일 오류로 수정 대상 구현체 7개가 전부 표시된다.

### Step 5 — 플래너 구현체 7개 업데이트
각 플래너에서 `field.FindBestIsolatedEnemy()` 패턴을 `snapshot.BestIsolatedEnemy` 패턴으로 교체한다. 플래너별 특이사항:

- `EscapePlanner`: `field.ComputeEnemyPressureCenter()` → `snapshot.EnemyPressureCenter`
- `PeelForWeakAllyPlanner`, `SupportWeakAllyPlanner`: `field.FindMostPressuredAlly()` → `snapshot.MostPressuredAlly`
- `RegroupPlanner`: `field.ComputeAllyTeamCenter()` → `snapshot.AllyTeamCenter`
- `EngageNearestPlanner`: 직접 거리 계산 사용 시 `snapshot.LivingEnemies` 참조로 변경
- `AssassinatePlanner`: `snapshot.BestIsolatedEnemy`
- `IdlePlanner`: 필드 참조 없음, 시그니처만 변경

### Step 6 — BattleSimulationManager에 스냅샷 빌드 삽입
`BuildAllExecutionPlans()` 시작부에 스냅샷 빌드 호출을 추가하고, 플래너 호출 시 `_fieldView` 대신 `snapshot`을 전달한다.

```csharp
void BuildAllExecutionPlans()
{
    var snapshot = BattleFieldSnapshot.Build(_runtimeUnits, aiTuning.parameterRadii);
    for (int i = 0; i < _runtimeUnits.Count; i++)
    {
        // ...
        plan = planner.Build(unit, snapshot);
    }
}
```

### Step 7 — BattleFieldView 정리
탐색 메서드가 모두 이동했으면 `BattleFieldView`에서 해당 메서드들을 제거한다. 런타임 유닛 목록 접근자(`GetAllUnits()`, `IsValidEnemyTarget()` 등 플래너가 아닌 다른 곳에서 쓰이는 메서드)는 유지한다. `BattleFieldView`가 비게 되면 제거를 검토하고, 유지가 필요하면 역할을 주석으로 명시한다.

### Step 8 — 회귀 검증
BattleScene 플레이 모드에서 전투를 5회 이상 실행하며 플래너 행동 패턴이 이전과 동일한지 확인한다. `BattleStatusGridUIManager`에서 각 유닛의 선택 행동(`top-scored action`)이 정상 표시되는지 확인한다.
