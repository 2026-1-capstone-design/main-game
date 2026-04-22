# 011. GladiatorAgent 관찰 공간 고정 슬롯 불안정성 해결

## 문제 상황

`GladiatorAgent`의 관찰 공간은 UnitNumber 기반 고정 슬롯으로 구성된다.

```
슬롯 구성 (85 floats):
  자신        [0..7]    8 floats
  teammate 0  [8..14]   7 floats  ← UnitNumber 오름차순 중 가장 작은 아군
  teammate 1  [15..21]  7 floats
  teammate 2  [22..28]  7 floats
  teammate 3  [29..35]  7 floats
  teammate 4  [36..42]  7 floats
  opponent 0  [43..49]  7 floats  ← UnitNumber 오름차순 중 가장 작은 적군
  opponent 1  [50..56]  7 floats
  opponent 2  [57..63]  7 floats
  opponent 3  [64..70]  7 floats
  opponent 4  [71..77]  7 floats
  opponent 5  [78..84]  7 floats
```

### 핵심 문제 1 — 유닛 사망 시 슬롯 이동

```csharp
// GladiatorAgent.CollectObservations() 내부 (단순화)
int slotIdx = 0;
foreach (BattleRuntimeUnit ally in GetLivingTeammates_Sorted())
{
    WriteUnitSlot(sensor, ally);   // slotIdx 0 → 1 → 2 ...
    slotIdx++;
}
// 남은 슬롯은 0으로 패딩
for (; slotIdx < TeammateSlots; slotIdx++)
    WriteZeroSlot(sensor);
```

teammate 3명이 전투 시작 시 슬롯 0·1·2를 차지한다. teammate[0]가 사망하면 살아 있는 teammate가 슬롯 0·1로 이동한다. 신경망 입장에서는 **이전에 슬롯 0에 있던 teammate가 슬롯 1로 순간 이동**한 것처럼 보이고, 이를 다른 상태(state)로 해석한다.

### 핵심 문제 2 — 행동 마스킹과 슬롯 불일치

```csharp
// WriteDiscreteActionMask() — 공격 행동 마스킹
for (int i = 0; i < OpponentSlots; i++)
{
    bool slotAlive = i < livingOpponents.Count;
    actionMasker.SetMask(0, AttackActionBase + i, !slotAlive);
}
```

행동 Branch 0의 `2~7`은 opponent 슬롯 0~5에 대응한다. 슬롯 이동이 발생하면 에이전트가 "슬롯 2 공격" 행동을 선택했을 때 실제로는 **다른 적**에게 공격이 가는 상황이 생긴다. 마스킹이 슬롯 채움 여부만 검사하지 대상의 일관성을 보장하지 않는다.

### 핵심 문제 3 — 관찰 슬롯 인덱스가 암묵적 계약

슬롯 레이아웃(인덱스 0 = 경기장 중심 상대좌표 x, 인덱스 4 = 공격력…)이 `CollectObservations()` 코드를 읽지 않으면 알 수 없다. `BehaviorParameters`의 `Space Size = 85`가 이 암묵적 계약의 유일한 문서다. 슬롯 순서를 바꾸면 기존에 학습된 모델이 즉시 무효화된다.

### 핵심 문제 4 — 비정규화 관찰값과 경기장 크기 결합

```csharp
sensor.AddObservation(relativePos.x);   // 예: -600 ~ +600 월드 단위
sensor.AddObservation(_selfUnit.AttackRange);  // 예: 80.0
```

관찰값이 월드 단위로 들어오므로 경기장 크기(`BattlefieldCollider.bounds`)가 바뀌면 동일 상황에서 다른 수치가 입력된다. 주석에 "에이전트가 AttackRange와 거리를 직접 비교할 수 있어야 한다"고 명시되어 있지만, 이 결정이 신경망 학습에 미치는 장기적 영향이 문서화되어 있지 않다.

---

## 해결 방안

### 단계 1 — 고정 UnitNumber 기반 슬롯 할당으로 변경

슬롯을 살아있는 유닛의 정렬 순서가 아니라 **UnitNumber 자체**에 고정한다.

```csharp
// 팀메이트 슬롯: UnitNumber 1~5 → 슬롯 0~4 고정
// 상대 슬롯:     UnitNumber 7~12 → 슬롯 0~5 고정

private void WriteTeammateSlots(VectorSensor sensor)
{
    for (int num = 1; num <= 5; num++)
    {
        if (num == _selfUnit.UnitNumber) continue;  // 자신 제외
        BattleRuntimeUnit u = _flowManager.GetUnitByNumber(num);
        if (u == null || u.IsCombatDisabled)
            WriteZeroSlot(sensor);  // 사망: 슬롯 이동 없음, 그 자리에 0
        else
            WriteUnitSlot(sensor, u);
    }
}
```

유닛 사망 시 해당 슬롯이 0으로 채워지지만 **슬롯 번호와 UnitNumber의 매핑은 에피소드 내내 고정**된다. 신경망은 "슬롯 2가 항상 UnitNumber 3의 자리"임을 일관되게 학습할 수 있다.

### 단계 2 — ObservationSchema 문서화 구조체

```csharp
// 신규: BattleObservationSchema.cs
public static class BattleObservationSchema
{
    public const int TotalSize = 85;

    // Self
    public const int SelfStart      = 0;   // 길이 8
    public const int SelfArenaDx    = 0;
    public const int SelfArenaDz    = 1;
    public const int SelfHealth     = 2;
    public const int SelfMaxHealth  = 3;
    public const int SelfAttack     = 4;
    public const int SelfRange      = 5;
    public const int SelfMoveSpeed  = 6;
    public const int SelfAtkCooldown = 7;

    // Teammates: unit UnitNumber 1~5 (자신 제외 → 4슬롯)
    // Opponents: unit UnitNumber 7~12 → 6슬롯
    // 슬롯당 7 floats: dx, dz, health, maxHealth, attack, range, moveSpeed
    public const int SlotSize       = 7;
    public const int TeammateStart  = 8;
    public const int OpponentStart  = TeammateStart + 5 * SlotSize;   // 43

    public static int TeammateSlotStart(int unitNumber) => TeammateStart + (unitNumber - 1) * SlotSize;
    public static int OpponentSlotStart(int unitNumber) => OpponentStart + (unitNumber - 7) * SlotSize;
}
```

`CollectObservations()`는 이 상수를 참조하여 슬롯을 채운다. 슬롯 레이아웃을 바꾸면 상수만 수정하면 되고, 변경 내용이 컴파일 타임에 검증된다.

### 단계 3 — 정규화 여부 명시적 결정 및 문서화

현재의 비정규화 방식을 유지하되, `BattleObservationBuilder`에서 경기장 스케일(`arenaExtentsMin`)로 나누는 옵션을 선택적으로 적용하고 Inspector 플래그로 제어한다.

```csharp
[Header("Observation")]
public bool normalizePositions = false;
// false: 에이전트가 AttackRange ↔ 거리를 직접 비교 (현재 방식)
// true:  경기장 반경 기준 [-1, 1] 정규화 (신경망 일반화에 유리)
```

에이전트가 경기장 크기와 무관하게 일반화되어야 한다면 `true`로, 수치 직접 비교를 원한다면 `false`로 설정한다.

---

## 기대 효과

| 항목 | 현재 | 개선 후 |
|---|---|---|
| 유닛 사망 시 슬롯 이동 | 발생 — 신경망이 다른 상태로 해석 | UnitNumber 고정 슬롯으로 이동 없음 |
| 공격 행동 ↔ 슬롯 대상 일관성 | 슬롯 이동 시 다른 적 타격 가능 | UnitNumber 고정으로 항상 동일 대상 |
| 슬롯 레이아웃 파악 | 코드 전체 읽어야 파악 | `BattleObservationSchema` 상수에서 확인 |
| 경기장 크기 변경 시 영향 | 관찰값 스케일 변화로 재학습 필요 | `normalizePositions`로 명시적 선택 |
| 관찰 공간 단위 테스트 | 실제 유닛 MonoBehaviour 필요 | `BattleObservationBuilder.Build()` stub 테스트 가능 |

---

## 영향 범위

### 수정 대상 파일
- `Assets/Scripts/TrainingScripts/GladiatorAgent.cs`  
  — `CollectObservations()`: 슬롯 할당 방식을 UnitNumber 고정으로 전면 변경  
  — `WriteDiscreteActionMask()`: 마스킹 로직을 UnitNumber 기반으로 맞춤
- `Assets/Scripts/BattleScene/BattleSceneFlowManager.cs`  
  — `GetUnitByNumber(int unitNumber)` public 메서드 추가 (단순 딕셔너리 조회)

### 신규 생성 파일
```
Assets/Scripts/TrainingScripts/
  BattleObservationSchema.cs
  BattleObservationBuilder.cs
```

### 학습된 모델에 미치는 영향
**슬롯 고정 방식 변경은 기존 학습 가중치를 무효화한다.** 슬롯 레이아웃이 바뀌므로 현재 훈련된 `.onnx` 파일을 그대로 사용할 수 없다. 변경 적용 후 처음부터 재훈련 필요. 단, 이 변경은 신경망의 관찰 일관성을 근본적으로 개선하므로 장기 학습 안정성이 향상된다.

### 위험도
**중간.** 코드 변경 자체는 단순하지만 훈련 재시작이 필요하다. 변경 전 현재 모델의 체크포인트를 보관해 두는 것을 권장한다.

---

## 구현 단계

### Step 1 — 현재 모델 체크포인트 백업
훈련된 `.onnx` 파일과 `results/` 디렉토리를 별도 경로에 복사해 둔다. 이 변경 후에는 기존 가중치를 재사용할 수 없다.

### Step 2 — BattleObservationSchema.cs 생성
`Assets/Scripts/TrainingScripts/BattleObservationSchema.cs`를 생성하고 해결 방안의 상수를 전부 정의한다. 파일 작성 후 `TotalSize` 상수가 실제 observation size와 일치하는지 `Debug.Assert(BattleObservationSchema.TotalSize == 85)`로 검증하는 테스트용 로그를 `GladiatorAgent.Initialize()`에 임시 추가한다.

### Step 3 — BattleSceneFlowManager에 GetUnitByNumber() 추가
`BattleSceneFlowManager.cs`에 다음 메서드를 추가한다.

```csharp
public BattleRuntimeUnit GetUnitByNumber(int unitNumber)
{
    if (_unitByNumber.TryGetValue(unitNumber, out var unit))
        return unit;
    return null;
}
```

`_unitByNumber` 딕셔너리는 `BootstrapFromPayload()` 중 유닛 스폰 시점에 채운다. Step 1(SpawnResult)이 먼저 완료되어 있다면 `_spawnResult.ByUnitNumber`를 그대로 활용한다.

### Step 4 — GladiatorAgent.CollectObservations() 슬롯 고정 방식으로 전환
기존 `GetLivingTeammates_Sorted()` 반복문을 제거하고 UnitNumber 순회로 교체한다.

**아군 슬롯 (UnitNumber 1~5, 자신 제외):**
```csharp
for (int num = 1; num <= 5; num++)
{
    if (num == _selfUnit.UnitNumber) continue;
    BattleRuntimeUnit u = _flowManager.GetUnitByNumber(num);
    if (u == null || u.IsCombatDisabled) WriteZeroSlot(sensor);
    else WriteUnitSlot(sensor, u);
}
```

**적군 슬롯 (UnitNumber 7~12):**
```csharp
for (int num = 7; num <= 12; num++)
{
    BattleRuntimeUnit u = _flowManager.GetUnitByNumber(num);
    if (u == null || u.IsCombatDisabled) WriteZeroSlot(sensor);
    else WriteUnitSlot(sensor, u);
}
```

### Step 5 — WriteDiscreteActionMask() UnitNumber 기반으로 수정
공격 행동 마스킹을 UnitNumber 기반으로 교체하여 슬롯 대상과 행동 인덱스가 항상 일치하도록 한다.

```csharp
public override void WriteDiscreteActionMask(IDiscreteActionMask actionMasker)
{
    int slotIdx = 0;
    for (int num = 7; num <= 12; num++, slotIdx++)
    {
        BattleRuntimeUnit u = _flowManager.GetUnitByNumber(num);
        bool canAttack = u != null && !u.IsCombatDisabled;
        actionMasker.SetMask(0, AttackActionBase + slotIdx, !canAttack);
    }
}
```

### Step 6 — normalizePositions Inspector 플래그 추가 (선택)
`GladiatorAgent`에 `[Header("Observation")] public bool normalizePositions = false;` 필드를 추가한다. `WriteUnitSlot()` 내부에서 좌표 값을 `_arenaExtentsMin`으로 나누는 분기를 추가한다. 기본값 `false`로 두어 기존 동작을 유지한다.

### Step 7 — 관찰 크기 일관성 검증
변경 후 `BehaviorParameters`의 `Space Size`가 여전히 85인지 확인한다. 자신(8) + 아군 슬롯(4 × 7 = 28, 자신 제외이므로) + 적군 슬롯(6 × 7 = 42) = 78이 아니라, 원래 설계가 teammate 5슬롯(자신 포함해서 5명 중 자신 제외 → 실제 4개)인지 재검토한다. 슬롯 수가 바뀌면 `BehaviorParameters.Space Size`와 `BattleObservationSchema.TotalSize`를 동시에 수정하고 모델을 처음부터 훈련한다.

### Step 8 — 단기 훈련 실행으로 안정성 확인
`mlagents-learn`으로 10만 스텝 단기 훈련을 실행한다. TensorBoard에서 `Episode Length`가 이전 대비 점진적으로 증가하고 `Cumulative Reward`가 수렴 방향으로 움직이는지 확인한다. 슬롯 이동 버그가 사라지면 동일 스텝 수에서 더 높은 수렴 속도를 기대할 수 있다.
