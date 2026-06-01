# BattleScene Agent Usage

이 문서는 BattleScene ML-Agent 학습 실행, BuiltInAI 데모 녹화, 진단 메트릭 확인 절차를 설명한다.

## BuiltInAI 데모 녹화

`GladiatorBehavior.yaml`은 `behavioral_cloning.demo_path`로 `Assets/Demos/GladiatorBuiltInAI_v5.demo`를 참조한다. 이 파일은 Unity Editor에서 `DemonstrationRecorder`와 `GladiatorAgent.Heuristic()`의 BuiltInAI 경로를 사용해 녹화한다.

Observation/action contract가 바뀌면 기존 `.demo` 파일은 재사용할 수 없다. 현재 contract는 observation size `147`, continuous action `2`, discrete branch `[3, 4, 6]`이다.

현재 action contract:

- Continuous 0: anchor strafe
- Continuous 1: anchor forward
- Discrete Branch 0: Command, size 3 (`0=Move`, `1=Attack`, `2=Withdraw`)
- Discrete Branch 1: Strategy, size 4
- Discrete Branch 2: enemy anchor slot, size 6

녹화 전 준비는 다음 순서로 진행한다.

1. Unity Editor에서 학습용 씬 또는 `TrainPlatform`이 들어간 씬을 연다.
2. `GladiatorAgent`가 붙어 있는 agent GameObject를 선택한다.
3. `GladiatorAgent` 컴포넌트에서 `Use Built In Ai Heuristic`을 켠다.
4. `Behavior Parameters` 컴포넌트에서 `Behavior Type`을 `Heuristic Only`로 바꾼다.
5. `Assets/Demos/` 폴더가 없으면 Project 창에서 `Assets` 아래에 `Demos` 폴더를 만든다.

그 다음 같은 agent GameObject에 `Demonstration Recorder` 컴포넌트를 추가하고 다음 값으로 설정한다.

- `Record`: 켬
- `Num Steps To Record`: `200000`
- `Demonstration Name`: `GladiatorBuiltInAI_v5`
- `Demonstration Directory`: `Assets/Demos/`

녹화는 Editor Play로 시작한다. Play 중 agent가 키보드 입력 없이 BuiltInAI처럼 이동하고 공격하는지 확인한다. `Num Steps To Record`가 목표 step에 도달하면 녹화가 끝나며, 자동으로 멈추지 않으면 충분한 step이 지난 뒤 Stop을 누른다. 종료 후 `Assets/Demos/GladiatorBuiltInAI_v5.demo`가 생성되었는지 확인한다.

녹화 후에는 학습/일반 실행 설정으로 반드시 복원한다.

1. `Demonstration Recorder`의 `Record`를 끈다.
2. `Behavior Parameters`의 `Behavior Type`을 `Default`로 되돌린다.
3. `GladiatorAgent`의 `Use Built In Ai Heuristic`을 끈다.

이 복원 과정을 빠뜨리면 이후 학습이 trainer 정책 대신 heuristic 입력으로만 진행되거나, 의도하지 않게 데모 파일을 계속 덮어쓸 수 있다.

## ML-Agents CLI 가이드

ML-Agents 학습은 Python 패키지의 `mlagents-learn` CLI로 실행한다. 기본 형식은 trainer 설정 YAML과 run id를 넘기는 방식이다.

```bash
mlagents-learn <trainer-config-file> --run-id=<run-identifier>
```

Unity Editor에서 학습할 때는 `--env`를 생략하고 명령을 먼저 실행한 뒤, Editor에서 `TrainingScene`을 Play한다.

```bash
mlagents-learn Assets/ML-Agents/GladiatorBehavior.yaml --run-id=gladiator_smooth_builtinai_001
```

### TrainingScene이 포함된 executable 만들기

빌드 학습에서 말하는 "training scene이 포함된 executable"은 `TrainingScene`이 Build Settings에 포함된 Windows/macOS/Linux standalone Player 빌드다. Python trainer는 이 executable을 `--env`로 실행하고, Unity Player 안의 ML-Agents runtime과 통신한다.

Unity Editor에서 만드는 기본 절차는 다음과 같다.

1. Unity Editor에서 `Assets/Scenes/TrainingScene.unity`를 연다.
2. `File > Build Profiles` 또는 `File > Build Settings`를 연다.
3. Scene 목록에 `TrainingScene`이 포함되어 있는지 확인한다.
4. 포함되어 있지 않으면 `Add Open Scenes`로 추가한다.
5. 학습 전용 빌드라면 `TrainingScene`만 enabled로 두는 편이 가장 단순하다.
6. Platform을 `Windows, Mac, Linux` 계열 standalone target으로 설정한다.
7. Windows에서 학습할 경우 target architecture는 보통 `x86_64`를 사용한다.
8. `Build`를 눌러 출력 위치를 `Build/GladiatorTraining` 같은 폴더로 지정한다.
9. 빌드가 끝나면 Windows 기준으로 `Build/GladiatorTraining/gladiator.exe` 같은 실행 파일이 생성된다.

그 다음 `mlagents-learn`에서 이 실행 파일을 `--env`로 넘긴다.

```bash
mlagents-learn Assets/ML-Agents/GladiatorBehavior.yaml \
    --run-id=gladiator_smooth_001 \
    --env=./Build/GladiatorTraining/gladiator.exe \
    --num-envs=4 \
    --no-graphics
```

주의할 점은 `--env`에 넘기는 경로가 폴더가 아니라 실제 실행 파일이어야 한다는 점이다. Windows 빌드는 `.exe`, macOS 빌드는 `.app`, Linux 빌드는 실행 가능한 player 파일을 가리켜야 한다.

자동 빌드가 필요하면 Unity batchmode에서 build script를 실행하는 방식을 사용한다. 이 경우 프로젝트 안에 `BuildScripts.BuildWindows64` 같은 Editor 전용 static method를 먼저 만들어야 한다.

```bash
"C:\Program Files\Unity\Hub\Editor\6000.0.71f1\Editor\Unity.exe" \
    -batchmode \
    -quit \
    -projectPath "D:\Projects\2026-capstone1-sogang-univ\main-game" \
    -buildTarget StandaloneWindows64 \
    -executeMethod BuildScripts.BuildWindows64 \
    -logFile ".temp/build-windows.log"
```

이 프로젝트에 아직 build script가 없다면 먼저 Editor UI로 빌드하는 방식이 안전하다. batchmode 빌드는 CI나 반복 학습용 executable을 계속 새로 만들 때 추가하는 편이 좋다.

기존 checkpoint에서 이어서 학습하려면 같은 `--run-id`와 함께 `--resume`을 사용한다.

```bash
mlagents-learn Assets/ML-Agents/GladiatorBehavior.yaml \
    --run-id=gladiator_smooth_001 \
    --resume
```

같은 `run-id`의 기존 결과를 지우고 새로 시작하려면 `--force`를 사용한다.

```bash
mlagents-learn Assets/ML-Agents/GladiatorBehavior.yaml \
    --run-id=gladiator_smooth_001 \
    --force
```

### 자주 쓰는 CLI 옵션

- `--run-id`
  - 학습 결과 디렉토리 이름이다. checkpoint, TensorBoard 로그, export된 model이 이 id 아래에 저장된다.
- `--env`
  - 학습에 사용할 Unity executable 경로다. Editor 학습에서는 생략한다.
- `--num-envs`
  - 빌드 실행 파일을 여러 개 띄워 병렬 학습한다. Editor 학습에는 보통 사용하지 않는다.
- `--no-graphics`
  - 빌드 환경을 headless로 실행한다. 관찰이 vector 중심이면 학습 속도를 높이는 데 유리하다.
- `--resume`
  - 같은 `run-id`의 checkpoint에서 학습을 이어간다.
- `--force`
  - 같은 `run-id`의 기존 결과를 덮어쓰고 처음부터 학습한다.
- `--seed`
  - trainer와 환경 seed를 고정해 재현성을 높인다.
- `--time-scale`
  - Unity engine time scale을 조정한다. 이 프로젝트에서는 battle tick 수를 `battleTicksPerEnvironmentStep`으로 별도 관리하므로, `--time-scale`은 "엔진 실행 속도"로 보고 battle logic step 배수와 혼동하지 않아야 한다.

### YAML 설정 예시

CLI 옵션 일부는 YAML의 `engine_settings`, `env_settings`로도 관리할 수 있다.

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
  env_path: ./Build/GladiatorTraining/gladiator.exe
  env_args: null
  base_port: 5005
  num_envs: 4
  timeout_wait: 60
  seed: 123
```

CLI 인자와 YAML 설정을 동시에 쓰면 실행 시 넘긴 CLI 인자가 더 눈에 잘 띄므로, 실험마다 바뀌는 값은 CLI로 두고 기본값은 YAML에 두는 편이 관리하기 쉽다.

## 학습 진단 메트릭 확인

`GladiatorAgent`는 episode 종료 시 `StatsRecorder`를 통해 전투 진단 메트릭을 기록한다. 병렬 학습 환경과 높은 `timeScale`에서는 Scene view나 Unity Console로 행동을 직접 확인하기 어렵기 때문에, TensorBoard의 scalar를 우선 확인한다.

TensorBoard 실행:

```bash
tensorboard --logdir results
```

브라우저에서 다음 주소를 연다.

```text
http://localhost:6006
```

`Scalars` 탭의 검색창에 다음 prefix를 입력한다.

```text
Combat/
```

전략 분포는 `Combat/StrategyShare/*`와 `Combat/StrategyMaintenance`를 우선 확인한다. Anchor 관련 지표는 enemy slot 기준으로 기록된다.
