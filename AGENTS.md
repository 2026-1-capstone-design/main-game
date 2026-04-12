# 프로젝트 지침

## 코드 작성

- 새 모듈(class, interface, enum 등)을 추가할 때는 해당 클래스의 책임과 사용 의도를 간략히 설명하는 주석을 함께 남깁니다.
- 코드를 작성할 때 코드만으로 드러나지 않는 컨텍스트, 예를 들어 관계가 먼 파일과의 의존 관계나 구현 의사결정의 히스토리는 주석으로 남깁니다.

## 커밋 메시지

- (prefix): (message) 형식으로 작성합니다.

# Gladiator Combat Simulator (Tentative Title)

This project is a **Gladiator Combat Simulation Game** developed using Unity. It focuses on strategic combat through the combination of various weapons, skills, and character traits.

## Guidelines for Agents

- **Unity ML-Agents:** When working on features related to Unity ML-Agents, always use the `find-docs` skill to retrieve the latest documentation from the `/unity-technologies/ml-agents` library.
- **로컬 빌드 검증:** C# 파일 삭제 후 Unity가 생성한 `.csproj`에 stale `Compile Include`가 남을 수 있으므로, `dotnet build Assembly-CSharp.csproj --no-restore`를 직접 실행하지 말고 `python tools/repair_unity_csproj.py --build`를 사용합니다. stale include 확인만 필요하면 `python tools/repair_unity_csproj.py --check`를 사용합니다.

## 파일 경로 출력

- 답변할 때 파일 이름을 참조로 출력할 때, 전체 경로를 포함하지 말고, 파일 이름만 출력합니다. 예를 들어, `Assets/Scripts/BattleScene/Simulation/BattleCombatSystem.cs`로 출력하는게 아니라, `BattleCombatSystem.cs`로 출력합니다.
