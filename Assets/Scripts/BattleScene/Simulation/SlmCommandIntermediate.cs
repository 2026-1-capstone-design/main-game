using UnityEngine;

// SLM 응답 DTO와 시뮬레이션 사이를 매개하는 명령 IR.
// DTO 스키마가 바뀌어도 시뮬레이션은 IR만 의존한다.

public enum SlmCommandKind
{
    None = 0,
    Attack = 1,
    Move = 2,
    Skill = 3,
    Wait = 4,
    DeferSkill = 5,
    NoSkill = 6,
}

// 이동 의도. 어떤 좌표 산출 알고리즘을 선택할지의 기준이 된다.
public enum SlmMoveSubtype
{
    None = 0,
    ApproachOpponent = 1,
    Escape = 2,
    Help = 3,
    HoldFront = 4,
}

// 이동 스타일. direct는 직선, flank는 적 군집 회피 우회 경로.
public enum SlmMoveStyle
{
    Direct = 0,
    Flank = 1,
}

// 한 유닛에게 내려지는 명령 하나. SLM 응답의 action[i].sequence[j] 한 칸에 대응한다.
public readonly struct SlmUnitCommand
{
    public readonly BattleRuntimeUnit Actor;
    public readonly SlmCommandKind Kind;

    // 공격/스킬 타겟. Move 시에는 목표 유닛(approachOpponent/help 등)을 가리킨다.
    public readonly BattleRuntimeUnit Target;

    public readonly SlmMoveSubtype MoveSubtype;
    public readonly SlmMoveStyle MoveStyle;

    // wait/deferSkill의 지속 시간(초). 적용 시 actionPolicy 범위로 clamp한다.
    public readonly float DurationSec;

    public SlmUnitCommand(
        BattleRuntimeUnit actor,
        SlmCommandKind kind,
        BattleRuntimeUnit target,
        SlmMoveSubtype moveSubtype,
        SlmMoveStyle moveStyle,
        float durationSec
    )
    {
        Actor = actor;
        Kind = kind;
        Target = target;
        MoveSubtype = moveSubtype;
        MoveStyle = moveStyle;
        DurationSec = durationSec;
    }
}
