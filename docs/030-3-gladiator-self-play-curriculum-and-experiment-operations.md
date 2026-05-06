# 030. Gladiator 자기대전 커리큘럼 및 실험 운영 정비

## 개요

(선행: 030-2 완료 후 시작)  
`030-1`, `030-2`에서 전략 표현과 보상 정의를 정비한 뒤에는 이를 안정적으로 학습시키는 curriculum과 실험 운영 체계가 필요하다. 이 플랜은 self-play lesson을 역할별로 분리하고, TensorBoard와 episode metric 기반으로 실험 종료/승급 조건을 명확히 해 “뭉치지 않는 전략 분화”를 재현 가능한 실험 단위로 운영하는 것을 목표로 한다.

## 문제 상황

### 현재 curriculum은 tactic mode 수준에 머문다

```yaml
# Assets/Configs/ML-Agents/Stage_1_smooth_self_play.yaml
environment_parameters:
  tactic_mode:
    curriculum:
      - name: ChaseOnly
        completion_criteria:
          measure: progress
          behavior: GladiatorSmooth
          signal_smoothing: true
          min_lesson_length: 2000
          threshold: 0.4
          require_reset: true
        value: 0.0
      - name: ChaseAndFlank
        completion_criteria:
          measure: progress
          behavior: GladiatorSmooth
          signal_smoothing: true
          min_lesson_length: 4000
          threshold: 0.7
          require_reset: true
        value: 1.0
```

**문제 1 — 전술 허용 범위만 늘고 과제 분리가 약하다**  
현재 lesson은 `tactic_mode` 값으로 가능한 path/anchor 종류만 늘린다. 하지만 “도주 적 추격”, “포커싱 아군 peel”, “backline assassinate”처럼 서로 다른 실패 구조를 가진 과제를 따로 검증하지 않는다.

**문제 2 — lesson 승급 기준이 progress 하나에 묶여 있다**  
`progress` 기반 threshold만으로는 뭉침 완화나 역할 분화 성공을 측정하기 어렵다. 결과적으로 승급은 하지만 원한 행동은 안 생길 수 있다.

### 현재 실험 메트릭은 전투 공통 지표 위주다

```csharp
// Assets/Scripts/BattleScene/Agent/README.md
Combat/DamageDealtRatio
Combat/AttackIntent
Combat/AttackOpportunityUseRate
Combat/InRangeNoAttack
Combat/OutOfRangeAttack
Combat/TargetSwitch
Combat/MeanTargetDistance
```

**문제 3 — 전투 효율은 보이지만 역할 분화 성공 여부는 안 보인다**  
현재 지표만으로는 chase가 개선됐는지, regroup 남용이 줄었는지, peel이 실제로 동작하는지 알기 어렵다.

### episode 초기 조건 다양화 설계가 드러나지 않는다

```csharp
// Assets/Scripts/BattleScene/Agent/TrainingBootstrapper.cs
// reset과 payload 생성은 담당하지만, 역할별 scenario 분기 의도는 코드 구조에 드러나지 않음
```

**문제 4 — curriculum이 trainer YAML에만 있고 scenario 구성 전략은 코드에서 독립되지 않는다**  
초기 배치나 상대 상태를 역할 학습용으로 제어하려면 environment side 시나리오 구성이 필요하다. 현재는 전술 학습 목적의 scenario preset이 명시적으로 드러나지 않는다.

## 해결 방안

### 단계 1 — lesson을 역할 과제 중심으로 분리

```yaml
environment_parameters:
  lesson_mode:
    curriculum:
      - name: ChaseTarget
        value: 0.0
      - name: FinishLowHp
        value: 1.0
      - name: PeelFocusedAlly
        value: 2.0
      - name: SplitPressure3v3
        value: 3.0
```

`tactic_mode`만으로 허용 행동을 늘리는 대신, environment가 역할별 과제를 명확히 드러내는 lesson을 제공하게 한다.

### 단계 2 — lesson별 scenario preset을 bootstrapper에서 분기

```csharp
public enum GladiatorLessonMode
{
    ChaseTarget = 0,
    FinishLowHp = 1,
    PeelFocusedAlly = 2,
    SplitPressure3v3 = 3,
}

private BattleStartPayload BuildLessonPayload(GladiatorLessonMode lessonMode)
```

`TrainingBootstrapper` 또는 payload factory에서 lesson mode에 따라 초기 체력, 배치, 팀 규모, 적 상태를 다르게 만든다.

### 단계 3 — lesson별 metric gate를 정의

```yaml
completion_criteria:
  measure: reward
  behavior: GladiatorSmooth
  threshold: 0.8
```

trainer의 lesson 승급은 기본 reward를 쓰되, 병행해서 TensorBoard 기준 수동 gate를 둔다.

- `ChaseTarget`: `MeanTargetDistance`, `AssassinateAbortRate`
- `FinishLowHp`: finish conversion, `OutOfRangeAttack`
- `PeelFocusedAlly`: `PeelCompletionRate`, ally survival
- `SplitPressure3v3`: regroup overstay, target diversity

### 단계 4 — 실험 결과 기록 템플릿 정리

```markdown
| Run ID | Lesson | 변경점 | 주요 지표 | 판정 |
|---|---|---|---|---|
| gladiator_role_001 | ChaseTarget | RoleBranch 추가 | MeanTargetDistance 하락 | 유지 |
```

실험마다 어떤 계약과 reward를 썼는지 기록하지 않으면 정책 퇴행 원인을 추적하기 어렵다. `.temp/experiments` 또는 기존 결과 기록 방식과 연결 가능한 템플릿을 만든다.

### 단계 5 — BuiltInAI pretrain과 self-play를 분리 운영

```yaml
environment_parameters:
  opponent_mode: 0.0  # pretrain
```

기초 움직임/공격 연결은 BuiltInAI 상대 pretrain으로, 역할 분화는 self-play lesson으로 분리한다. 각 단계의 책임을 나누지 않으면 self-play 초기에 무의미한 집결 정책이 오래 남을 수 있다.

## 기대 효과

| 항목 | 현재 | 개선 후 |
|---|---|---|
| lesson 설계 | tactic mode 중심 | 역할 과제 중심 |
| 승급 판단 | progress 단일 기준 | lesson별 역할 지표 병행 |
| 초기 상황 제어 | 일반 reset 위주 | 과제별 scenario preset |
| 실험 비교 | run별 차이 추적 어려움 | 지표와 변경점이 템플릿으로 정리됨 |
| 학습 단계 분리 | pretrain/self-play 경계가 약함 | 기초기술과 전략분화를 별도 운영 |

## 영향 범위

### 수정 대상 파일

- `Assets/Configs/ML-Agents/Stage_1_smooth_self_play.yaml`
  — lesson mode 중심 curriculum으로 재구성
- `Assets/Scripts/BattleScene/Agent/TrainingBootstrapper.cs`
  — lesson mode 환경 파라미터 읽기 및 reset 흐름 연동
- `Assets/Scripts/BattleScene/Agent/TrainingBattlePayloadFactory.cs`
  — lesson별 초기 배치와 체력 상태 분기
- `Assets/Scripts/BattleScene/Agent/README.md`
  — 새 lesson과 관측/지표 해석 가이드 업데이트
- `Assets/Scripts/BattleScene/Agent/GladiatorAgentEpisodeMetrics.cs`
  — lesson별 핵심 지표 flush 지원

### 신규 생성 파일

```
Assets/Scripts/BattleScene/Agent/Training/GladiatorLessonMode.cs
Assets/Scripts/BattleScene/Agent/Training/GladiatorLessonScenarioBuilder.cs
.temp/experiments/gladiator-role-curriculum-template.md
```

### 위험도
**낮음.** 학습 운영과 scenario 구성을 정리하는 작업이므로 런타임 전투 로직 자체를 크게 바꾸지 않는다. 다만 trainer config와 실험 파이프라인이 바뀌므로 운영 실수 가능성은 있다.

## 구현 단계

### Step 1 — lesson mode enum과 scenario builder 추가

`GladiatorLessonMode.cs`, `GladiatorLessonScenarioBuilder.cs`를 생성해 과제 단위 시나리오를 코드 구조로 드러낸다.

```csharp
public static BattleStartPayload Build(
    GladiatorLessonMode lessonMode,
    TrainingBootstrapperContext context)
```

### Step 2 — bootstrapper에서 lesson mode 읽기

`TrainingBootstrapper.cs`가 `lesson_mode` environment parameter를 읽고 payload builder에 전달하도록 수정한다.

```csharp
var lessonMode = (GladiatorLessonMode)Academy.Instance.EnvironmentParameters.GetWithDefault("lesson_mode", 0f);
```

### Step 3 — self-play YAML을 과제 중심 curriculum으로 재작성

`Stage_1_smooth_self_play.yaml`에서 `tactic_mode` lesson만으로 승급하던 구조를 `lesson_mode` 중심으로 교체한다.

```yaml
environment_parameters:
  lesson_mode:
    curriculum:
      - name: ChaseTarget
        value: 0.0
      - name: FinishLowHp
        value: 1.0
```

### Step 4 — 역할별 metric gate와 로그 템플릿 정리

`README.md`와 `.temp/experiments/gladiator-role-curriculum-template.md`를 작성해 어떤 지표를 어떤 lesson에서 봐야 하는지 문서화한다.

```markdown
| Lesson | 필수 지표 | 실패 패턴 |
|---|---|---|
| PeelFocusedAlly | PeelCompletionRate, AllyWin | regroup 고착 |
```

### Step 5 — pretrain / self-play 실행 경로 분리 문서화

BuiltInAI 상대 pretrain과 self-play run id, 체크포인트 사용 규칙을 `README.md`에 정리한다.

```yaml
opponent_mode: 0.0
team_size: 1.0
```

### Step 6 — 검증

- lesson mode별로 reset 시나리오가 의도한 초기 상태를 만드는지 Editor에서 확인한다.
- 각 lesson에서 TensorBoard scalar가 문서화한 지표를 실제로 내보내는지 확인한다.
- 동일 코드로 pretrain과 self-play run을 분리 실행해 설정 충돌이 없는지 확인한다.
- 실험 템플릿으로 최소 1회 기록을 남겨 비교 흐름이 작동하는지 확인한다.
