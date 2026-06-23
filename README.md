# Morituri

Deep RL & SLM 기반 검투사단 육성 시뮬레이션 게임.  
서강대학교 2026 캡스톤디자인 프로젝트.

---

## 개요

**Morituri**는 고대 로마 검투사 문화를 배경으로 한 검투사단 육성 시뮬레이션 게임이다.

플레이어는 검투사단의 단장이 되어 매일 검투사를 관리하고, 무기와 장신구를 구매·판매하며, 스쿼드를 편성해 전투에 내보낸다. 전투는 원형 경기장에서 6 대 6 실시간 전투로 진행되며, 검투사들은 강화학습 기반 전투 AI에 따라 자율적으로 이동·공격·후퇴·전술 선택을 수행한다.

플레이어는 전투 중 자연어 또는 음성 명령으로 전투에 개입할 수 있다. 파인튜닝된 소형 언어 모델은 명령을 해석해 대상 검투사와 행동을 결정하고, 각 검투사의 성격과 충성도에 따라 복종·불복 여부와 대사를 생성한다.

---

## 주요 특징

- **검투사단 운영 루프**
  - 하루 단위로 검투사 관리, 장비 구매·판매, 스쿼드 편성, 전투, 보상 정산이 진행된다.

- **강화학습 기반 6 대 6 전투 AI**
  - Unity ML-Agents 기반 전투 AI가 전장 상태를 관측하고 이동, 공격, 후퇴, 전술 선택을 수행한다.
  - 검투사의 성격에 따라 공격적, 회피적, 이타적, 이기적인 행동 차이가 나타나도록 설계하였다.

- **자연어·음성 명령 처리**
  - 플레이어는 전투 중 텍스트 또는 음성으로 명령을 입력할 수 있다.
  - Whisper STT와 파인튜닝된 Gemma 계열 SLM을 활용해 자연어 명령을 구조화된 전투 행동으로 변환한다.

- **성격 기반 복종·불복 및 대사 시스템**
  - 검투사는 명령을 항상 수행하지 않고, 성격과 충성도에 따라 복종하거나 불복할 수 있다.
  - 명령 처리 결과에 따라 각 검투사의 페르소나에 맞는 대사가 출력된다.

- **장비 및 성장 시스템**
  - 검투사는 무기와 장신구를 장착할 수 있다.
  - 무기는 스탯과 무기 스킬을 제공하며, 장신구는 전투 효과와 전략적 선택지를 제공한다.

---

## 기술 스택

| 항목 | 내용 |
| --- | --- |
| Engine | Unity 6 (6000.0.71f1), URP |
| Language | C# |
| UI | UGUI, TextMeshPro |
| AI / RL | Unity ML-Agents, MA-POCA |
| LLM / SLM | Gemma 4 E4B, Gemini API |
| Speech | Whisper STT |
| Data | ScriptableObject, JSON Save Data |
| Formatting | CSharpier |
| Collaboration | GitHub, Notion, Discord, Google Drive |

---

## 아키텍처

Morituri는 씬 기반 구조로 구성되어 있으며, BootScene에서 생성된 핵심 서비스가 게임 전체 흐름을 관리한다.

```text
BootScene
  └─ Persistent Services
     ├─ SessionManager
     ├─ RandomManager
     ├─ AudioManager
     ├─ SaveGameService
     └─ Content / Runtime Managers

MainScene
  └─ MainFlowManager
     ├─ Gladiator Management
     ├─ Squad Management
     ├─ Inventory
     ├─ Market
     ├─ Account / EOD
     └─ Battle Preparation

BattleScene
  └─ BattleSimulationManager
     ├─ Reinforcement Learning Combat AI
     ├─ Natural Language Order Pipeline
     ├─ Unit Runtime State
     └─ Real-time Battle UI
```

---

## 게임 흐름

```text
Title
  ↓
Main
  ├─ 검투사 관리
  ├─ 스쿼드 편성
  ├─ 인벤토리 관리
  ├─ 상점 구매·판매
  └─ 전투 준비
       ↓
Battle
  ├─ 아군 배치
  ├─ 6 대 6 실시간 전투
  ├─ 자연어·음성 명령
  └─ 전투 결과 정산
       ↓
Main
```

---

## 시작하기

### 요구 사항

- Unity 6 (6000.0.71f1)
- .NET SDK
- Git LFS
- CSharpier

### 설치

```bash
git clone https://github.com/2026-1-capstone-design/main-game
cd main-game

git lfs install
dotnet tool restore
```

STT 사용 시 https://huggingface.co/ggerganov/whisper.cpp/tree/main 에서 ggml-large-v3-turbo-q8_0.bin 다운로드 후 /Assets/StreamingAssets/Whisper 에 이동.

Unity Hub에서 `main-game` 폴더를 프로젝트로 열면 패키지가 자동으로 복원된다.

### 실행

Unity Editor에서 다음 씬을 실행한다.

```text
Assets/Scenes/BootScene.unity
```

기본 씬 흐름은 다음과 같다.

```text
BootScene → TitleScene → MainScene → BattleScene
```

빌드 시에도 `BootScene`이 첫 번째 씬으로 등록되어 있어야 한다.

---

## 개발 규칙

### 코드 포맷

C# 코드는 CSharpier를 사용해 포맷한다.

```bash
dotnet csharpier format Assets/Scripts
```

포맷 체크:

```bash
dotnet csharpier check Assets/Scripts
```

### 브랜치 / PR

- 기능 단위 브랜치에서 작업한다.
- PR 생성 전 `main` 기준 rebase를 수행한다.
- Unity Scene / Prefab 변경이 필요한 기능은 관련 `.unity`, `.prefab`, `.meta` 파일을 함께 커밋한다.

---

## 주요 디렉터리

```text
Assets/
  Content/              # ScriptableObject 기반 게임 데이터
  Images/               # UI, 무기, 장신구, 스킬 이미지
  Scenes/               # Boot, Title, Main, Battle 씬
  Scripts/
    BootScripts/        # 세션, 세이브, 타이틀 관련 로직
    MainScripts/        # 메인 씬, UI, 매니저, 팩토리
    BattleScene/        # 전투 시뮬레이션, 전투 UI, 명령 처리
```

---

## 결과

본 프로젝트는 서강대학교 2026 캡스톤디자인 프로젝트로 개발되었으며, 교내 캡스톤디자인 경진대회에서 은상을 수상하였다.

---

## Team Epoch

| 이름 | 역할 |
| --- | --- |
| 최진호 | 팀장 |
| 김진혁 | 팀원 |
| 이상협 | 팀원 |
| 이동협 | 팀원 |
| 이수찬 | 팀원 |
| 전상훈 | 팀원 |
