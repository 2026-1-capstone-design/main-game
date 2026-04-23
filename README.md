# Gladiator Manager — 2026 Sogang Capstone

LLM 기반 전술 AI를 탑재한 글래디에이터 전략 시뮬레이션 게임.  
서강대학교 2026 캡스톤 프로젝트.

---

## 개요

플레이어는 글래디에이터 팀을 운영하는 매니저 역할을 맡는다. 매일 시장에서 전사를 영입하고, 장비를 구매하며, 연구 트리를 통해 팀을 강화한다. 전투는 LLM이 각 유닛의 행동을 실시간으로 결정하는 자율 시뮬레이션으로 진행된다.

### 주요 특징

- **경제 루프**: 골드 관리, 시장 거래, 인력 운영이 하루 단위 사이클로 진행
- **전투 시뮬레이션**: 15틱/초 속도로 구동되는 결정론적 전투 엔진
- **LLM 전술 AI**: 각 유닛이 전장 상황을 분석해 7가지 행동 중 최적을 선택; LLM 프록시 서버와 연동
- **유닛 커스터마이징**: 클래스, 특성(Trait), 퍽(Perk), 시너지, 무기 스킬 조합

---

## 기술 스택

| 항목     | 내용                                   |
| -------- | -------------------------------------- |
| 엔진     | Unity 6 (6000.0.71f1), URP             |
| 언어     | C#                                     |
| UI       | UGUI + TextMeshPro                     |
| 입력     | Unity Input System                     |
| 네트워킹 | UnityWebRequest (LLM 프록시 HTTP POST) |
| 데이터   | ScriptableObject 기반 콘텐츠           |
| 포맷터   | CSharpier                              |

---

## 아키텍처

씬 기반 레이어드 모놀리스 구조로, 부트 씬에서 생성된 싱글톤 서비스가 전체 씬에 걸쳐 지속된다.

```
BootScene  ─▶  Persistent Services (SessionManager, RandomManager, AudioManager ...)
                        │
                        ▼
MainScene  ─▶  MainFlowManager
               ├── Market / Recruit
               ├── Gladiator Roster & Equipment
               ├── Research Tree
               └── Battle Preparation
                        │
                        ▼ BattleStartPayload
BattleScene ─▶  BattleSimulationManager
                ├── Unit AI Decision (9 params → 7 actions)
                ├── LLM Order Integration
                └── Real-time Combat UI
```

---

## 시작하기

### 요구 사항

- Unity 6 (6000.0.71f1)
- .NET SDK (CSharpier 설치용)

### 설치

```bash
# 저장소 클론
git clone <repo-url>
cd main-game

# CSharpier 포맷터 설치
dotnet tool install csharpier
```

Unity Hub에서 `main-game` 폴더를 프로젝트로 열면 패키지가 자동으로 복원된다.

### 실행

Unity Editor에서 `Assets/Scenes/BootScene.unity`를 열고 Play 버튼을 누른다.  
씬 로드 순서: `BootScene` → `MainScene` (자동) → `BattleScene` (전투 시작 시).

---

## 기여

기여 방법, 브랜치 전략, 코드 스타일, PR 프로세스는 [`CONTRIBUTING.md`](CONTRIBUTING.md)를 참고한다.
