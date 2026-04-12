# BattleScene Agent Training Flow

이 디렉토리는 전투 시뮬레이션을 Unity ML-Agents 학습 환경으로 노출하는 코드를 담습니다.

핵심 목표는 `Agent`의 decision, 전투 시뮬레이션 tick, reward/terminal 정산 시점을 같은 step 경계에 맞추는 것입니다. 일반 전투 씬은 `BattleSimulationManager.Update()`가 시간 누적으로 전투를 진행하지만, 학습 씬은 `TrainingBootstrapper`가 step을 직접 소유합니다.

## 아키텍처 구조

현재 `Agent` 디렉토리는 "시스템 종류"가 아니라 "계층 책임" 기준으로 정리되어 있습니다.

```text
Shared -> Action / Observation / Reward -> Agent -> TrainingSetup
```

- `Shared`
  - 하위 계층과 상위 계층이 함께 사용하는 가장 낮은 공용 재료 계층입니다.
  - `Environment`: 전장 좌표계, roster 정렬, runtime unit 해석, curriculum source 같은 환경 모델을 둡니다.
  - `Combat`: action과 reward가 함께 해석하는 전술 명령과 tactical context를 둡니다.
  - `Logging`: episode metrics, field description attribute처럼 하위 레이어에서도 안전하게 쓸 수 있는 로깅 보조 타입을 둡니다.

- `Action`
  - action contract와 policy action schema를 정의하고, ML-Agents action buffer를 전투 입력으로 변환합니다.
  - `GladiatorAction`, `GladiatorAgentActionParser`, `GladiatorAgentActionSink`, `GladiatorActionSchema`가 여기에 속합니다.

- `Observation`
  - observation schema와 feature builder를 소유합니다.
  - `GladiatorObservationBuilder`가 `Shared`의 환경/전투 데이터를 읽어 고정 크기 observation을 구성합니다.

- `Reward`
  - reward config, evaluator, role-specific reward rules를 둡니다.
  - action step 결과와 tactical feature를 받아 reward shaping과 종료 판정을 계산합니다.

- `Agent`
  - Unity ML-Agents API adapter 계층입니다.
  - `GladiatorAgent`는 observation/action/reward 계층을 조합해서 `CollectObservations`, `WriteDiscreteActionMask`, `OnActionReceived`에 연결합니다.
  - `BattleSceneGladiatorAgentBinder`와 `GladiatorAgentInferenceConfig`는 inference/runtime binding 진입점입니다.

- `TrainingSetup`
  - 학습 씬 bootstrap, episode reset, payload 생성, agent binding, Academy stepping을 담당하는 최상위 계층입니다.
  - `TrainingBootstrapper`, `TrainingEpisodeController`, `TrainingBattlePayloadFactory`, `TrainingAgentBinder`가 여기에 속합니다.
  - `Curriculum` 하위 폴더는 trainer YAML과 공유하는 curriculum 환경 파라미터 이름을 둡니다.

## 의존 방향

- `Shared`는 어느 계층에서도 참조할 수 있습니다.
- `Action`, `Observation`, `Reward`는 `Shared`를 사용하지만 서로를 최소한으로만 참조합니다.
- `Agent`는 `Action`, `Observation`, `Reward`, `Shared`를 조합합니다.
- `TrainingSetup`은 `Agent`를 포함한 모든 하위 계층을 조립할 수 있습니다.
- 반대로 `Agent`는 `TrainingSetup` 구체 타입을 직접 참조하지 않습니다. 학습 환경이 필요한 값은 `IGladiatorCurriculumSource` 같은 shared abstraction을 통해 받습니다.

## 주요 파일

- `Agent/GladiatorAgent.cs`
  - ML-Agents `Agent` 구현체입니다.
  - `CollectObservations()`에서 고정 크기 observation을 수집합니다.
  - `WriteDiscreteActionMask()`에서 사망한 대상 슬롯을 막습니다.
  - `OnActionReceived()`에서 continuous 월드 이동과 discrete 전투 command를 `BattleRuntimeUnit`의 외부 제어 입력으로 변환합니다.

- `TrainingSetup/TrainingBootstrapper.cs`
  - 학습 episode를 생성하고 reset합니다.
  - preset에서 `BattleStartPayload`를 만들고, 전투 유닛을 spawn한 뒤 agent와 runtime unit을 연결합니다.
  - `BattleSimulationManager`의 `Update()` 자동 진행을 끄고, 학습 step에서 직접 battle tick을 진행합니다.

- `Observation/GladiatorObservationSchema.cs`
  - observation vector의 고정 크기와 슬롯 구성을 정의합니다.

- `Action/GladiatorActionSchema.cs`
  - continuous action과 discrete action branch의 의미와 크기를 정의합니다.

- `Shared/Environment/GladiatorStateRosterView.cs`
  - agent 관점에서 팀원/상대 슬롯을 안정적으로 정렬하고 조회하는 read model입니다.

## Academy란 무엇인가

ML-Agents의 `Academy`는 Unity 환경의 전역 학습 루프를 관리하는 singleton입니다. 환경 step timing, reset, training/inference 설정, Python trainer와의 통신 경계를 소유합니다.

기본적으로 ML-Agents는 `FixedUpdate` 시점에 내부 stepper를 통해 `Academy.EnvironmentStep()`을 자동 호출합니다. 이 자동 stepping을 그대로 두면 Unity engine step 기준으로 observation 수집, action 적용, reward 전송이 진행됩니다.

현재 전투 시뮬레이션은 별도의 battle tick을 가집니다. 따라서 학습 씬에서는 Academy 자동 stepping과 `BattleSimulationManager.Update()` 자동 tick이 동시에 돌면 다음 문제가 생길 수 있습니다.

- agent decision 1회와 battle tick 1회의 대응이 깨집니다.
- 한 렌더/물리 프레임 안에서 battle tick이 여러 번 진행될 수 있습니다.
- 같은 action이 의도치 않게 여러 battle tick 동안 재사용될 수 있습니다.
- reward와 terminal 판단이 ML-Agents environment step 경계와 어긋날 수 있습니다.

이 문제를 피하기 위해 `TrainingBootstrapper`는 Academy step과 battle tick을 명시적으로 정렬합니다.

## 현재 학습 step 순서

`TrainingBootstrapper.FixedUpdate()`에서 학습 step은 다음 순서로 진행됩니다.

1. 하나의 `TrainingBootstrapper`가 Academy step driver가 됩니다.
2. driver가 `Academy.Instance.EnvironmentStep()`을 한 번 호출합니다.
3. 활성화된 모든 `TrainingBootstrapper`가 각자의 `BattleSimulationManager.StepSimulationTicks(...)`를 호출합니다.
4. 전투 종료 또는 timeout이면 해당 episode를 reset합니다.

현재 기본 계약은 다음과 같습니다.

```text
1 Academy environment step = 1 battle simulation tick
```

이 비율은 `TrainingBootstrapper.battleTicksPerEnvironmentStep`으로 조정할 수 있습니다. 값을 2 이상으로 올리면 의도적인 action repeat/frame skip이 됩니다. 이 경우 한 번의 ML-Agents environment step에서 같은 action이 여러 battle tick 동안 적용된다는 점을 전제로 reward와 관찰 품질을 다시 검토해야 합니다.

## 왜 static Academy driver를 쓰는가

`TrainingScene`에는 병렬 학습을 위해 여러 전투 환경과 여러 `TrainingBootstrapper`가 존재할 수 있습니다. 하지만 `Academy`는 전역 singleton이므로 `EnvironmentStep()`은 전체 Unity 환경 기준으로 한 번만 호출되어야 합니다.

각 bootstrapper가 자기 `FixedUpdate()`에서 `EnvironmentStep()`을 직접 호출하면, 한 Unity frame 안에서 Academy step이 환경 수만큼 중복 진행됩니다. 그래서 현재 코드는 다음 static 상태를 둡니다.

- `ActiveBootstrappers`
  - 현재 활성화된 학습 환경 목록입니다.

- `_academyStepDriver`
  - `EnvironmentStep()` 호출 권한을 가진 단일 bootstrapper입니다.

- `_academySteppingWasAutomatic`
  - 학습 씬이 끝날 때 ML-Agents 자동 stepping 설정을 원래 상태로 복구하기 위한 저장값입니다.

driver는 Academy를 한 번 step한 뒤 `ActiveBootstrappers`를 순회하면서 각 전투 환경의 battle tick을 진행합니다. 이 구조 덕분에 여러 병렬 전투 환경도 같은 Academy step 경계 안에서 전진합니다.

## 자동 stepping 제어

ML-Agents 문서 예시에서는 자동 stepping을 끄는 개념을 `DisableAutomaticStepping()`으로 설명합니다. 이 프로젝트에서 사용하는 `com.unity.ml-agents` 4.0.2에서는 공개 API로 `Academy.Instance.AutomaticSteppingEnabled` 프로퍼티를 사용합니다.

현재 구현은 driver 획득 시 다음처럼 동작합니다.

```csharp
_academySteppingWasAutomatic = Academy.Instance.AutomaticSteppingEnabled;
Academy.Instance.AutomaticSteppingEnabled = false;
```

driver가 비활성화되면 저장했던 값을 다시 넣어 원래 설정을 복구합니다.

## BattleSimulationManager와의 관계

학습 씬에서는 `TrainingBootstrapper.Start()`에서 다음 호출로 전투 시뮬레이션의 `Update()` 기반 자동 진행을 끕니다.

```csharp
battleSimulationManager.SetAutoStepInUpdate(false);
```

그 뒤 전투 진행은 오직 `StepSimulationTicks(battleTicksPerEnvironmentStep)` 경로로만 일어납니다. 이렇게 해야 action 적용, battle tick, reward/terminal 정산을 학습 step 단위로 추적할 수 있습니다.

일반 전투 씬에서는 `autoStepInUpdate` 기본값이 `true`이므로 기존처럼 `Time.deltaTime` 누적 방식으로 전투가 진행됩니다.

## Episode reset 흐름

episode가 끝나는 조건은 크게 두 가지입니다.

- `battleSimulationManager.IsBattleFinished`
  - 한 팀이 전멸하거나 전투 승패가 결정된 경우입니다.

- `BattleTimeoutTicks`
  - 전투가 너무 오래 지속되어 강제 reset해야 하는 경우입니다.

reset 시 `TrainingBootstrapper`는 모든 agent에 `EndEpisode()`를 호출하고, 새 payload와 spawn position을 만든 뒤 같은 씬 안에서 전투 환경을 다시 bootstrap합니다. 이후 agent와 새 runtime unit을 다시 연결합니다.

## BuiltInAI 데모 녹화

`Stage_1_smooth.yaml`은 `behavioral_cloning.demo_path`로 `Assets/Demos/GladiatorBuiltInAI_v5.demo`를 참조합니다. 이 파일은 Unity Editor에서 `DemonstrationRecorder`와 `GladiatorAgent.Heuristic()`의 BuiltInAI 경로를 사용해 녹화합니다.

Observation/action contract가 바뀌면 기존 `.demo` 파일은 재사용할 수 없습니다. 현재 contract는 observation size `134`, continuous action `2`, discrete branch `[2, 3, 4, 3, 6]`입니다.

녹화 전 준비는 다음 순서로 진행합니다.

1. Unity Editor에서 학습용 씬 또는 `TrainPlatform`이 들어간 씬을 엽니다.
2. `GladiatorAgent`가 붙어 있는 agent GameObject를 선택합니다.
3. `GladiatorAgent` 컴포넌트에서 `Use Built In Ai Heuristic`을 켭니다.
4. `Behavior Parameters` 컴포넌트에서 `Behavior Type`을 `Heuristic Only`로 바꿉니다.
5. `Assets/Demos/` 폴더가 없으면 Project 창에서 `Assets` 아래에 `Demos` 폴더를 만듭니다.

그 다음 같은 agent GameObject에 `Demonstration Recorder` 컴포넌트를 추가하고 다음 값으로 설정합니다.

- `Record`: 켬
- `Num Steps To Record`: `200000`
- `Demonstration Name`: `GladiatorBuiltInAI_v5`
- `Demonstration Directory`: `Assets/Demos/`

녹화는 Editor Play로 시작합니다. Play 중 agent가 키보드 입력 없이 BuiltInAI처럼 이동하고 공격하는지 확인합니다. `Num Steps To Record`가 목표 step에 도달하면 녹화가 끝나며, 자동으로 멈추지 않으면 충분한 step이 지난 뒤 Stop을 누릅니다. 종료 후 `Assets/Demos/GladiatorBuiltInAI_v5.demo`가 생성되었는지 확인합니다.

녹화 후에는 학습/일반 실행 설정으로 반드시 복원합니다.

1. `Demonstration Recorder`의 `Record`를 끕니다.
2. `Behavior Parameters`의 `Behavior Type`을 `Default`로 되돌립니다.
3. `GladiatorAgent`의 `Use Built In Ai Heuristic`을 끕니다.

이 복원 과정을 빠뜨리면 이후 학습이 trainer 정책 대신 heuristic 입력으로만 진행되거나, 의도하지 않게 데모 파일을 계속 덮어쓸 수 있습니다.

## ML-Agents CLI 가이드

ML-Agents 학습은 Python 패키지의 `mlagents-learn` CLI로 실행합니다. 기본 형식은 trainer 설정 YAML과 run id를 넘기는 방식입니다.

```bash
mlagents-learn <trainer-config-file> --run-id=<run-identifier>
```

Unity Editor에서 학습할 때는 `--env`를 생략하고 명령을 먼저 실행한 뒤, Editor에서 `TrainingScene`을 Play합니다.

```bash
mlagents-learn Assets/Configs/ML-Agents/Stage_1_smooth.yaml --run-id=gladiator_smooth_builtinai_001
```

`Stage_1_smooth.yaml`은 BuiltInAI 상대 1v1 pretrain 검증 전용입니다. 이 단계에서는 `self_play` 설정을 쓰지 않습니다. 이어서 Agent끼리 self-play를 진행할 때는 별도 trainer config를 사용합니다.

```bash
mlagents-learn Assets/Configs/ML-Agents/Stage_1_smooth_self_play.yaml --run-id=gladiator_smooth_selfplay_001
```

### TrainingScene이 포함된 executable 만들기

빌드 학습에서 말하는 "training scene이 포함된 executable"은 `TrainingScene`이 Build Settings에 포함된 Windows/macOS/Linux standalone Player 빌드입니다. Python trainer는 이 executable을 `--env`로 실행하고, Unity Player 안의 ML-Agents runtime과 통신합니다.

Unity Editor에서 만드는 기본 절차는 다음과 같습니다.

1. Unity Editor에서 `Assets/Scenes/TrainingScene.unity`를 엽니다.
2. `File > Build Profiles` 또는 `File > Build Settings`를 엽니다.
3. Scene 목록에 `TrainingScene`이 포함되어 있는지 확인합니다.
4. 포함되어 있지 않으면 `Add Open Scenes`로 추가합니다.
5. 학습 전용 빌드라면 `TrainingScene`만 enabled로 두는 편이 가장 단순합니다.
6. Platform을 `Windows, Mac, Linux` 계열 standalone target으로 설정합니다.
7. Windows에서 학습할 경우 target architecture는 보통 `x86_64`를 사용합니다.
8. `Build`를 눌러 출력 위치를 `Build/GladiatorTraining` 같은 폴더로 지정합니다.
9. 빌드가 끝나면 Windows 기준으로 `Build/GladiatorTraining/GladiatorTraining.exe` 같은 실행 파일이 생성됩니다.

그 다음 `mlagents-learn`에서 이 실행 파일을 `--env`로 넘깁니다.

```bash
mlagents-learn Assets/Configs/ML-Agents/Stage_1_smooth.yaml \
    --run-id=gladiator_smooth_001 \
    --env=./Build/GladiatorTraining/GladiatorTraining.exe \
    --num-envs=4 \
    --no-graphics
```

주의할 점은 `--env`에 넘기는 경로가 폴더가 아니라 실제 실행 파일이어야 한다는 점입니다. Windows 빌드는 `.exe`, macOS 빌드는 `.app`, Linux 빌드는 실행 가능한 player 파일을 가리켜야 합니다.

자동 빌드가 필요하면 Unity batchmode에서 build script를 실행하는 방식을 사용합니다. 이 경우 프로젝트 안에 `BuildScripts.BuildWindows64` 같은 Editor 전용 static method를 먼저 만들어야 합니다.

```bash
"C:\Program Files\Unity\Hub\Editor\6000.0.71f1\Editor\Unity.exe" \
    -batchmode \
    -quit \
    -projectPath "D:\Projects\2026-capstone1-sogang-univ\main-game" \
    -buildTarget StandaloneWindows64 \
    -executeMethod BuildScripts.BuildWindows64 \
    -logFile ".temp/build-windows.log"
```

이 프로젝트에 아직 build script가 없다면 먼저 Editor UI로 빌드하는 방식이 안전합니다. batchmode 빌드는 CI나 반복 학습용 executable을 계속 새로 만들 때 추가하는 편이 좋습니다.

기존 checkpoint에서 이어서 학습하려면 같은 `--run-id`와 함께 `--resume`을 사용합니다.

```bash
mlagents-learn Assets/Configs/ML-Agents/Stage_1_smooth.yaml \
    --run-id=gladiator_smooth_001 \
    --resume
```

같은 `run-id`의 기존 결과를 지우고 새로 시작하려면 `--force`를 사용합니다.

```bash
mlagents-learn Assets/Configs/ML-Agents/Stage_1_smooth.yaml \
    --run-id=gladiator_smooth_001 \
    --force
```

### 자주 쓰는 CLI 옵션

- `--run-id`
  - 학습 결과 디렉토리 이름입니다. checkpoint, TensorBoard 로그, export된 model이 이 id 아래에 저장됩니다.

- `--env`
  - 학습에 사용할 Unity executable 경로입니다. Editor 학습에서는 생략합니다.

- `--num-envs`
  - 빌드 실행 파일을 여러 개 띄워 병렬 학습합니다. Editor 학습에는 보통 사용하지 않습니다.

- `--no-graphics`
  - 빌드 환경을 headless로 실행합니다. 관찰이 vector 중심이면 학습 속도를 높이는 데 유리합니다.

- `--resume`
  - 같은 `run-id`의 checkpoint에서 학습을 이어갑니다.

- `--force`
  - 같은 `run-id`의 기존 결과를 덮어쓰고 처음부터 학습합니다.

- `--seed`
  - trainer와 환경 seed를 고정해 재현성을 높입니다.

- `--time-scale`
  - Unity engine time scale을 조정합니다. 이 프로젝트에서는 battle tick 수를 `battleTicksPerEnvironmentStep`으로 별도 관리하므로, `--time-scale`은 "엔진 실행 속도"로 보고 battle logic step 배수와 혼동하지 않아야 합니다.

### YAML 설정 예시

CLI 옵션 일부는 YAML의 `engine_settings`, `env_settings`로도 관리할 수 있습니다.

```yaml
engine_settings:
  width: 84
  height: 84
  quality_level: 0
  time_scale: 20
  target_frame_rate: -1
  capture_frame_rate: 60
  no_graphics: true

env_settings:
  env_path: ./Build/GladiatorTraining/GladiatorTraining.exe
  env_args: null
  base_port: 5005
  num_envs: 4
  timeout_wait: 60
  seed: 123
```

CLI 인자와 YAML 설정을 동시에 쓰면 실행 시 넘긴 CLI 인자가 더 눈에 잘 띄므로, 실험마다 바뀌는 값은 CLI로 두고 기본값은 YAML에 두는 편이 관리하기 쉽습니다.

## 학습 진단 메트릭 확인

`GladiatorAgent`는 episode 종료 시 `StatsRecorder`를 통해 전투 진단 메트릭을 기록합니다. 병렬 학습 환경과 높은 `timeScale`에서는 Scene view나 Unity Console로 행동을 직접 확인하기 어렵기 때문에, TensorBoard의 scalar를 우선 확인합니다.

TensorBoard 실행:

```bash
tensorboard --logdir results
```

브라우저에서 다음 주소를 엽니다.

```text
http://localhost:6006
```

`Scalars` 탭의 검색창에 다음 prefix를 입력합니다.

```text
Combat/
```

## 유지보수 시 주의점

- `Academy.EnvironmentStep()` 호출 지점을 여러 곳에 추가하지 않습니다.
- 학습 씬에서 `BattleSimulationManager.autoStepInUpdate`가 다시 켜지면 step 정렬이 깨집니다.
- `battleTicksPerEnvironmentStep`을 2 이상으로 바꾸면 action repeat 계약도 함께 문서화해야 합니다.
- reward를 `OnSimulationTicked`에서 즉시 experience로 간주하지 않습니다. ML-Agents experience 경계는 Academy environment step입니다.
- observation schema와 action schema를 바꾸면 Inspector의 `BehaviorParameters` 설정도 함께 맞춰야 합니다.
