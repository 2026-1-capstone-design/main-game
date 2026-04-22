# 012. BattleFieldView를 GladiatorAgent 관찰 소스로 활용

> **구현 시점**: 011과 동일 훈련 세션에서 함께 적용한다. 두 플랜 모두 `Space Size`를 바꾸고 재훈련을 요구하므로, 별도 세션으로 나누면 재훈련을 두 번 치른다.

---

## 문제 상황

### 1. GladiatorAgent 내부 중복 코드

`BattleFieldView`가 이미 제공하는 기능을 `GladiatorAgent`가 독립적으로 재구현하고 있다.

| GladiatorAgent 메서드 | BattleFieldView 동일 기능 |
|---|---|
| `GetTeammatesSorted()` | `GetLivingUnits(isEnemyTeam: false)` |
| `GetOpponentsSorted()` | `GetLivingUnits(isEnemyTeam: true)` |
| `GetNearestOpponentUnit()` | `FindNearestLivingEnemy(self)` |
| `GetDistToNearestOpponent()` | `FindNearestLivingEnemy()` + 거리 |
| `IsOutOfAttackRange(target)` | `!IsWithinEffectiveAttackDistance()` |

body radius 공식까지 동일하게 작성되어 있어(`BodyRadius + target.BodyRadius + AttackRange + 0.05f`), `BattleFieldView`의 동일 공식이 변경될 경우 `GladiatorAgent`만 다른 판정 기준을 쓰게 되는 무음 불일치가 발생한다.

### 2. 집계 전술 정보가 관찰 공간에 없음

현재 85개 float 관찰은 개별 유닛 스탯의 나열이다. 신경망이 "지금 어느 방향이 위협적인가", "어떤 적이 고립되어 있는가", "어떤 아군이 집중 공격받는가" 같은 전술 판단을 스스로 집계해야 한다.

`BattleFieldView`는 이 집계를 이미 수행한다.

- `ComputeEnemyPressureCenter(self)` — 거리 가중(quadratic falloff) 적군 위협 중심
- `FindBestIsolatedEnemy(self)` — `BattleParameterComputer`의 isolation score 기반 최적 암살 타겟
- `FindMostPressuredAlly(self)` — 집중 공격 횟수 + HP + 거리 가중 압박 아군

이 정보를 관찰에 추가하면 신경망이 학습해야 할 계산량을 줄이고 수렴 속도를 높일 수 있다.

### 3. BattleFieldView 접근 경로가 막혀 있음

```
GladiatorAgent._flowManager
  └─ BattleSceneFlowManager             (public: RuntimeUnits, BattlefieldCollider)
       └─ battleSimulationManager        (private SerializeField)
            └─ _fieldView               (private)  ← 접근 불가
```

`BattleSimulationManager._fieldView`는 `private`이고, `BattleSceneFlowManager`도 `SimulationManager`를 외부에 노출하지 않는다.

---

## 해결 방안

### 단계 1 — BattleFieldView 접근 경로 개방 (2줄)

```csharp
// BattleSimulationManager.cs
public BattleFieldView FieldView => _fieldView;

// BattleSceneFlowManager.cs
public BattleSimulationManager SimulationManager => battleSimulationManager;
```

Agent는 `_flowManager.SimulationManager.FieldView`로 접근한다.

### 단계 2 — 중복 메서드 제거 및 BattleFieldView로 교체

`GladiatorAgent`의 5개 유틸리티 메서드를 제거하고, 사용처를 `BattleFieldView` 호출로 교체한다.

```csharp
// 제거 대상
private List<BattleRuntimeUnit> GetTeammatesSorted()   { ... }
private List<BattleRuntimeUnit> GetOpponentsSorted()   { ... }
private BattleRuntimeUnit GetNearestOpponentUnit()     { ... }
private float GetDistToNearestOpponent()               { ... }
private bool IsOutOfAttackRange(BattleRuntimeUnit)     { ... }

// 교체 예시
// 기존: IsOutOfAttackRange(opp)
// 변경: !FieldView.IsWithinEffectiveAttackDistance(_selfUnit, opp)
```

### 단계 3 — 전술 관찰 슬롯 추가

기존 85 float 뒤에 8 float을 추가한다. 모두 에이전트 로컬 프레임(자신이 바라보는 방향 기준)으로 변환한다.

```
추가 관찰 (8 floats):
  [85..86] 적군 압력 중심 상대 방향 (x, z)  — ComputeEnemyPressureCenter()
  [87..88] 가장 고립된 적 상대 방향   (x, z)  — FindBestIsolatedEnemy()
  [89..90] 가장 압박받는 아군 방향    (x, z)  — FindMostPressuredAlly()
  [91..92] 아군 팀 중심 방향         (x, z)  — ComputeTeamCenter(false)
```

타겟이 없는 경우(전원 사망 등)는 `(0, 0)`으로 패딩한다.

`Space Size` 변경: **85 → 93**

```csharp
// BattleObservationSchema.cs (011 플랜에서 생성) 에 추가
public const int TacticalStart        = 85;
public const int EnemyPressureDx      = 85;
public const int EnemyPressureDz      = 86;
public const int IsolatedEnemyDx      = 87;
public const int IsolatedEnemyDz      = 88;
public const int PressuredAllyDx      = 89;
public const int PressuredAllyDz      = 90;
public const int AllyTeamCenterDx     = 91;
public const int AllyTeamCenterDz     = 92;
public const int TotalSize            = 93;  // 기존 85에서 변경
```

---

## 기대 효과

| 항목 | 현재 | 개선 후 |
|---|---|---|
| 공식 불일치 위험 | `IsOutOfAttackRange`가 `IsWithinEffectiveAttackDistance`와 독립 유지 | 단일 구현(`BattleFieldView`)으로 판정 기준 통일 |
| 압력 방향 인식 | 신경망이 6개 적 위치에서 직접 집계 | `EnemyPressureCenter` 벡터로 즉시 제공 |
| 고립 타겟 인식 | 신경망이 6개 적 × 5개 아군 거리 관계에서 직접 추론 | isolation score 결과를 방향 벡터로 직접 제공 |
| 압박 아군 인식 | 신경망이 집중 공격 여부를 추론 | `MostPressuredAlly` 방향 벡터로 보호 타겟 힌트 제공 |
| 중복 코드 | 5개 메서드 (약 60줄) | 제거 |

---

## 영향 범위

### 수정 대상 파일
- `Assets/Scripts/BattleScene/BattleSimulationManager.cs`  
  — `public BattleFieldView FieldView => _fieldView;` 추가
- `Assets/Scripts/BattleScene/BattleSceneFlowManager.cs`  
  — `public BattleSimulationManager SimulationManager => battleSimulationManager;` 추가
- `Assets/Scripts/TrainingScripts/GladiatorAgent.cs`  
  — `GetTeammatesSorted`, `GetOpponentsSorted`, `GetNearestOpponentUnit`, `GetDistToNearestOpponent`, `IsOutOfAttackRange` 제거  
  — `CollectObservations()` 말미에 전술 관찰 8 float 추가  
  — `WriteDiscreteActionMask()` 내 `IsOutOfAttackRange` → `FieldView.IsWithinEffectiveAttackDistance`로 교체  
  — `OnActionReceived()` 내 사거리 판정 동일하게 교체

### 수정 대상 에셋
- `TrainingScene.unity` — `BehaviorParameters.Space Size` 85 → 93

### 011 플랜에서 생성되는 파일에 추가
- `BattleObservationSchema.cs` — `TacticalStart` 이후 상수 및 `TotalSize = 93` 반영

### 학습된 모델에 미치는 영향
`Space Size` 변경으로 기존 `.onnx` 가중치는 무효화된다. **010과 동일 재훈련 세션에서 처리**하면 재훈련 비용이 추가로 발생하지 않는다.

### 위험도
**낮음.** API 노출(getter 2줄)과 중복 코드 제거는 동작을 바꾸지 않는다. 전술 관찰 추가는 기존 슬롯에 영향을 주지 않고 뒤에 붙는다. 재훈련 외에 기존 씬 설정 변경은 `Space Size` 수정 하나뿐이다.

---

## 구현 단계

### Step 1 — 011 플랜 선행 완료 확인
`BattleObservationSchema.cs` 생성과 UnitNumber 고정 슬롯 전환이 완료된 상태에서 시작한다. 이 플랜은 011 위에 쌓인다.

### Step 2 — BattleFieldView 접근 경로 개방
`BattleSimulationManager.cs`에 getter를 추가한다.

```csharp
public BattleFieldView FieldView => _fieldView;
```

`BattleSceneFlowManager.cs`에 getter를 추가한다.

```csharp
public BattleSimulationManager SimulationManager => battleSimulationManager;
```

`GladiatorAgent` 내부에서 FieldView 참조용 프로퍼티를 추가한다.

```csharp
private BattleFieldView FieldView => _flowManager?.SimulationManager?.FieldView;
```

### Step 3 — 중복 메서드 제거 및 교체
`GladiatorAgent`에서 5개 메서드를 제거하고 사용처를 교체한다. 교체 대상:

- `WriteDiscreteActionMask()` 내 `IsOutOfAttackRange(opp)` → `!FieldView.IsWithinEffectiveAttackDistance(_selfUnit, opp)`
- `OnActionReceived()` 내 사거리 판정 블록 → 동일하게 교체
- `GetAverageDistToOpponents()` 내 유닛 순회 → `FieldView.GetLivingUnits(!_selfUnit.IsEnemy)` 활용으로 간소화
- `CollectObservations()` 내 `GetTeammatesSorted()`, `GetOpponentsSorted()` → 010에서 이미 UnitNumber 순회로 교체되어 있으므로 해당 메서드 참조 없음. 메서드만 삭제한다.

컴파일 통과 확인 후 플레이 모드에서 마스킹이 정상 동작하는지 검증한다.

### Step 4 — BattleObservationSchema에 전술 슬롯 상수 추가
010에서 생성된 `BattleObservationSchema.cs`에 전술 슬롯 상수를 추가하고 `TotalSize`를 93으로 변경한다.

```csharp
public const int TacticalStart     = 85;
public const int EnemyPressureDx   = 85;
public const int EnemyPressureDz   = 86;
public const int IsolatedEnemyDx   = 87;
public const int IsolatedEnemyDz   = 88;
public const int PressuredAllyDx   = 89;
public const int PressuredAllyDz   = 90;
public const int AllyTeamCenterDx  = 91;
public const int AllyTeamCenterDz  = 92;
public const int TotalSize         = 93;
```

### Step 5 — CollectObservations()에 전술 관찰 추가
기존 85 float 수집 이후에 전술 블록을 추가한다. 모든 벡터는 `WorldToLocal()`로 에이전트 로컬 프레임으로 변환한다.

```csharp
// 적군 압력 중심
Vector3 pressureCenter = FieldView?.ComputeEnemyPressureCenter(_selfUnit) ?? _selfUnit.Position;
Vector2 pressureLocal  = WorldToLocal(pressureCenter - _selfUnit.Position);
sensor.AddObservation(pressureLocal.x);
sensor.AddObservation(pressureLocal.y);

// 가장 고립된 적
BattleRuntimeUnit isolated = FieldView?.FindBestIsolatedEnemy(_selfUnit);
Vector2 isolatedLocal = isolated != null
    ? WorldToLocal(isolated.Position - _selfUnit.Position)
    : Vector2.zero;
sensor.AddObservation(isolatedLocal.x);
sensor.AddObservation(isolatedLocal.y);

// 가장 압박받는 아군
BattleRuntimeUnit pressured = FieldView?.FindMostPressuredAlly(_selfUnit);
Vector2 pressuredLocal = pressured != null
    ? WorldToLocal(pressured.Position - _selfUnit.Position)
    : Vector2.zero;
sensor.AddObservation(pressuredLocal.x);
sensor.AddObservation(pressuredLocal.y);

// 아군 팀 중심
Vector3 allyCenter     = FieldView?.ComputeTeamCenter(_selfUnit.IsEnemy) ?? _selfUnit.Position;
Vector2 allyCenterLocal = WorldToLocal(allyCenter - _selfUnit.Position);
sensor.AddObservation(allyCenterLocal.x);
sensor.AddObservation(allyCenterLocal.y);
```

### Step 6 — BehaviorParameters Space Size 업데이트
`TrainingScene.unity`에서 `GladiatorAgent`에 연결된 `BehaviorParameters` 컴포넌트의 `Space Size`를 85 → 93으로 변경한다. `Debug.Assert(BattleObservationSchema.TotalSize == 93)`이 에디터에서 통과하는지 확인한다.

### Step 7 — 전체 관찰 카운트 검증
Play 모드 진입 시 ML-Agents가 observation mismatch 오류를 출력하지 않는지 확인한다. 오류가 없으면 관찰 벡터 크기가 `BehaviorParameters`와 일치한 것이다.

### Step 8 — 훈련 실행 및 전술 관찰 효과 확인
010과 함께 재훈련을 시작한다. TensorBoard에서 초기 수렴 속도(episode당 reward 상승 기울기)를 이전 훈련 결과와 비교한다. 전술 관찰이 도움이 된다면 동일 스텝 수 대비 더 높은 누적 보상을 기대할 수 있다.
