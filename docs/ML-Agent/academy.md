# BattleScene Agent Academy Step

이 문서는 BattleScene ML-Agent 학습 환경에서 Unity ML-Agents `Academy`와 전투 tick을 정렬하는 계약을 설명한다.

## Academy란 무엇인가

ML-Agents의 `Academy`는 Unity 환경의 전역 학습 루프를 관리하는 singleton이다. 환경 step timing, reset, training/inference 설정, Python trainer와의 통신 경계를 소유한다.

기본적으로 ML-Agents는 `FixedUpdate` 시점에 내부 stepper를 통해 `Academy.EnvironmentStep()`을 자동 호출한다. 이 자동 stepping을 그대로 두면 Unity engine step 기준으로 observation 수집, action 적용, reward 전송이 진행된다.

현재 전투 시뮬레이션은 별도의 battle tick을 가진다. 따라서 학습 씬에서 Academy 자동 stepping과 `BattleSimulationManager.Update()` 자동 tick이 동시에 돌면 다음 문제가 생길 수 있다.

- Academy environment step과 battle tick의 대응이 깨진다.
- 한 렌더/물리 프레임 안에서 battle tick이 여러 번 진행될 수 있다.
- `DecisionRequester.DecisionPeriod`와 별개로 같은 action이 의도치 않은 길이 동안 재사용될 수 있다.
- reward와 terminal 판단이 ML-Agents environment step 경계와 어긋날 수 있다.

이 문제를 피하기 위해 `TrainingBootstrapper`는 Academy step과 battle tick을 명시적으로 정렬한다.

## 현재 학습 step 순서

`TrainingBootstrapper.FixedUpdate()`에서 학습 step은 다음 순서로 진행된다.

1. 하나의 `TrainingBootstrapper`가 Academy step driver가 된다.
2. driver가 `Academy.Instance.EnvironmentStep()`을 한 번 호출한다.
3. 활성화된 모든 `TrainingBootstrapper`가 각자의 `BattleSimulationManager.StepSimulationTicks(...)`를 호출한다.
4. 전투 종료 또는 timeout이면 해당 episode를 reset한다.

현재 기본 계약은 다음과 같다.

```text
1 Academy environment step = 1 battle simulation tick
```

이 비율은 `TrainingBootstrapper.battleTicksPerEnvironmentStep`으로 조정할 수 있다. 값을 2 이상으로 올리면 한 번의 ML-Agents environment step에서 여러 battle tick을 진행하는 frame skip이 된다. 이 경우 reward와 관찰 품질을 다시 검토해야 한다.

단, 이 계약은 Academy step과 battle tick 사이의 계약이다. 정책 decision 주기는 agent prefab의 `DecisionRequester.DecisionPeriod`가 별도로 결정한다. 현재 `TrainPlatform` 학습 agent들의 `DecisionPeriod`는 5이므로 기본 설정에서는 다음처럼 동작한다.

```text
1 policy decision = 5 Academy environment steps = 5 battle simulation ticks
```

즉 `battleTicksPerEnvironmentStep`이 1이어도 같은 policy action은 기본적으로 5 battle tick 동안 유지된다. action repeat 길이는 `DecisionPeriod * battleTicksPerEnvironmentStep`으로 이해해야 한다.

## 왜 static Academy driver를 쓰는가

`TrainingScene`에는 병렬 학습을 위해 여러 전투 환경과 여러 `TrainingBootstrapper`가 존재할 수 있다. 하지만 `Academy`는 전역 singleton이므로 `EnvironmentStep()`은 전체 Unity 환경 기준으로 한 번만 호출되어야 한다.

각 bootstrapper가 자기 `FixedUpdate()`에서 `EnvironmentStep()`을 직접 호출하면, 한 Unity frame 안에서 Academy step이 환경 수만큼 중복 진행된다. 그래서 현재 코드는 `TrainingAcademyStepCoordinator`에 다음 상태를 둔다.

- `_environments`
  - 현재 활성화된 학습 환경 목록이다.
- `_driver`
  - `EnvironmentStep()` 호출 권한을 가진 단일 bootstrapper다.
- `_academySteppingWasAutomatic`
  - 학습 씬이 끝날 때 ML-Agents 자동 stepping 설정을 원래 상태로 복구하기 위한 저장값이다.

driver는 Academy를 한 번 step한 뒤 `_environments`를 순회하면서 각 전투 환경의 battle tick을 진행한다. 이 구조 덕분에 여러 병렬 전투 환경도 같은 Academy step 경계 안에서 전진한다.

## 자동 stepping 제어

ML-Agents 문서 예시에서는 자동 stepping을 끄는 개념을 `DisableAutomaticStepping()`으로 설명한다. 이 프로젝트에서 사용하는 `com.unity.ml-agents` 4.0.2에서는 공개 API로 `Academy.Instance.AutomaticSteppingEnabled` 프로퍼티를 사용한다.

현재 구현은 driver 획득 시 다음처럼 동작한다.

```csharp
_academySteppingWasAutomatic = Academy.Instance.AutomaticSteppingEnabled;
Academy.Instance.AutomaticSteppingEnabled = false;
```

driver가 비활성화되면 저장했던 값을 다시 넣어 원래 설정을 복구한다.

## BattleSimulationManager와의 관계

학습 씬에서는 `TrainingBootstrapper.Start()`에서 다음 호출로 전투 시뮬레이션의 `Update()` 기반 자동 진행을 끈다.

```csharp
battleSimulationManager.SetAutoStepInUpdate(false);
```

그 뒤 전투 진행은 오직 `StepSimulationTicks(battleTicksPerEnvironmentStep)` 경로로만 일어난다. 이렇게 해야 action 적용, battle tick, reward/terminal 정산을 학습 step 단위로 추적할 수 있다.

일반 전투 씬에서는 `autoStepInUpdate` 기본값이 `true`이므로 기존처럼 `Time.deltaTime` 누적 방식으로 전투가 진행된다.

## Episode reset 흐름

episode가 끝나는 조건은 크게 두 가지다.

- `battleSimulationManager.IsBattleFinished`
  - 한 팀이 전멸하거나 전투 승패가 결정된 경우다.
- `BattleTimeoutTicks`
  - 전투가 너무 오래 지속되어 강제 reset해야 하는 경우다.

reset 시 `TrainingEpisodeController`는 `TrainingAgentBinder.EndTrainingGroups(...)`를 통해 agent episode를 종료하고, 새 payload와 spawn position을 만든 뒤 같은 씬 안에서 전투 환경을 다시 bootstrap한다. 이후 agent와 새 runtime unit을 다시 연결한다.

현재 기본 설정은 POCA group reward를 사용한다. 정상 전투 종료에서는 ally/enemy `SimpleMultiAgentGroup`에 group reward를 더한 뒤 `EndGroupEpisode()`를 호출한다. timeout 또는 강제 reset은 interruption으로 취급해 `GroupEpisodeInterrupted()`를 호출한다. `EndEpisode()`는 POCA group reward를 쓰지 않거나 group이 준비되지 않은 경우의 fallback 경로다.

팀 outcome reward는 agent 성격 bias와 무관하게 팀 전체에 동일한 값으로 적용한다. 개별 damage, survival, strategy shaping만 personality mixing을 거치며, group reward는 `[-100, 100]` 범위로 clamp한다.

## 중간 사망 Agent와 group episode

POCA group reward를 사용하는 학습에서는 개별 유닛의 생존 종료 시점과 팀 episode 종료 시점을 구분한다.

예를 들어 전투 시간이 `0 ~ 100`이고 어떤 유닛이 `0 ~ 30`까지만 생존했다면, 해당 유닛은 tick 30에서 사망 또는 비활성화된다. 하지만 팀의 group episode는 전투가 끝나는 tick 100까지 계속된다.

```text
tick 0
- 유닛과 팀 episode 시작

tick 30
- 해당 유닛 사망
- 해당 유닛은 더 이상 observation/action을 받지 않음
- death penalty 또는 생존 관련 individual reward를 정산
- Agent GameObject를 비활성화하거나 group에서 제거되는 상태로 처리

tick 100
- 전투 승패 또는 timeout 확정
- group reward 정산
- EndGroupEpisode() 또는 GroupEpisodeInterrupted() 호출
```

따라서 중간에 사망한 유닛에게 곧바로 `EndEpisode()`를 호출하지 않는다. `EndEpisode()`는 해당 Agent의 `OnEpisodeBegin()`을 즉시 유발하므로, group training에서 같은 전투가 아직 진행 중인데 개별 Agent만 새 episode로 reset되는 문제가 생길 수 있다.

MA-POCA는 Agent가 episode 중간에 제거되거나 비활성화되어도 이후 group reward를 학습 신호로 사용할 수 있도록 설계되어 있다. 즉 tick 30에 죽은 유닛도 tick 100의 팀 승리 또는 패배 결과를 자신의 이전 행동에 대한 credit assignment로 받을 수 있다.

이 계약 때문에 개인 생존 보상은 tick 30에서 정산할 수 있지만, 팀 승패 보상은 tick 100의 group episode 종료 시점에 정산해야 한다.

## 유지보수 시 주의점

- `Academy.EnvironmentStep()` 호출 지점을 여러 곳에 추가하지 않는다.
- 학습 씬에서 `BattleSimulationManager.autoStepInUpdate`가 다시 켜지면 step 정렬이 깨진다.
- `battleTicksPerEnvironmentStep` 또는 `DecisionRequester.DecisionPeriod`를 바꾸면 action repeat 계약도 함께 문서화해야 한다.
- reward를 `OnSimulationTicked`에서 즉시 experience로 간주하지 않는다. ML-Agents experience 경계는 Academy environment step이다.
- observation schema와 action schema를 바꾸면 Inspector의 `BehaviorParameters` 설정도 함께 맞춰야 한다.
