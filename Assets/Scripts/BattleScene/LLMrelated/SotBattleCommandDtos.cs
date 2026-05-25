// SOT parser/dialog 레이어의 JSON 입출력 DTO를 정의한다.
// Newtonsoft.Json 직렬화를 기준으로 null 필드를 보존한다.
// 실행 명령으로 변환하지 않고 입력 조립과 로그 출력에만 사용한다.
// 후처리 결과 DTO는 dialog input과 미래 dispatcher의 중간 계약이다.

using System;
using Newtonsoft.Json;

[System.Serializable]
public sealed class SotParserOutputDto
{
    public string thinking;
    public SotDialogLineDto[] dialog;
    public SotActorActionDto[] action;
}

[System.Serializable]
public sealed class SotDialogLineDto
{
    public string unitId;
    public string text;
}

[System.Serializable]
public sealed class SotActorActionDto
{
    public string unitId;
    public SotFinalActionDto[] sequence;
}

[System.Serializable]
public sealed class SotDialogLayerResponseDto
{
    public SotDialogLineDto[] dialog;
}

[Serializable]
public struct BattleSkillRuntimeMetadata
{
    public WeaponSkillId skillId;
    public string skillDescription;
    public bool isSkillOnSelf;
    public bool isSkillOnOtherAlly;
    public bool isSkillAoe;
    public bool canSkillTargetDead;
}

[Serializable]
public sealed class SotParserRequestDto
{
    public SotParserInputDto input;
    public SotCommandAnalysisDto commandAnalysis;
}

[Serializable]
public sealed class SotParserInputDto
{
    public string command;
    public SotAreaSituationDto area_situation;
}

[Serializable]
public sealed class SotAreaSituationDto
{
    public SotAllyUnitDto[] allies;
    public SotEnemyUnitDto[] enemies;
}

[Serializable]
public sealed class SotAllyUnitDto
{
    public string unitId;
    public bool isAlive;
    public bool canBeTargeted;
    public bool isRanged;
    public float hpRatio;
    public float attackRatioToAvg;
    public int engagedByOpponentCount;
    public string teamFormationRole;
    public string skillDescription;
    public bool IsSkillOnSelf;
    public bool IsSkillOnOtherAlly;
    public bool isSkillAoe;
    public bool canSkillTargetDead;

    [JsonProperty(NullValueHandling = NullValueHandling.Include)]
    public string closestTargetableOpponent;

    [JsonProperty(NullValueHandling = NullValueHandling.Include)]
    public string farthestTargetableOpponent;

    [JsonProperty(NullValueHandling = NullValueHandling.Include)]
    public string closestAliveAlly;

    [JsonProperty(NullValueHandling = NullValueHandling.Include)]
    public string farthestAliveAlly;
}

[Serializable]
public sealed class SotEnemyUnitDto
{
    public string unitId;
    public bool isAlive;
    public bool canBeTargeted;
    public bool isRanged;
    public float hpRatio;
    public float attackRatioToAvg;
    public int engagedByOpponentCount;
    public string teamFormationRole;
}

[Serializable]
public sealed class SotCommandAnalysisDto
{
    public string analysisMode;
    public string[] allowedActors;
    public string[] allowedAttackTargets;
    public string[] validMoveToUnits;
    public string[] deadAllies;
    public string[] invalidUnits;
    public SotActionPolicyDto actionPolicy;
}

[Serializable]
public sealed class SotActionPolicyDto
{
    public int maxActionsPerActor;
    public string[] allowedActionTypes;
    public string[] allowedMoveSubtypes;
    public string[] allowedMovementTypes;
    public float waitDurationSecMin;
    public float waitDurationSecMax;
    public float skillControlDeferSecMin;
    public float skillControlDeferSecMax;
}

[Serializable]
public sealed class SotDialogLayerRequestDto
{
    public string originalCommand;
    public SotDialogActorInputDto[] actors;
}

[Serializable]
public sealed class SotDialogActorInputDto
{
    public string unitId;
    public int speechStyle;
    public string personalityDescription;
    public string sourceDialog;
    public string obedienceState;
    public string obeyedActionAdjustment;
    public string refusalSummary;
    public SotFinalActionDto[] finalActionSequence;
}

[Serializable]
public sealed class SotFinalActionDto
{
    public string type;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string subtype;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string movementType;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string to;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string target;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string description;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string mode;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? durationSec;
}

[Serializable]
public sealed class BattleCommandPostprocessResult
{
    public string originalCommand;
    public bool fallbackToDefaultMlAi;
    public string advisorLine;
    public BattleCommandFinalActorDto[] actors;
    public BattleCommandPostprocessDebugDto debug;
}

[Serializable]
public sealed class BattleCommandFinalActorDto
{
    public string unitId;
    public string obedienceState;
    public string mainActionCategory;
    public string sourceDialog;
    public string obeyedActionAdjustment;
    public string refusalSummary;
    public SotFinalActionDto[] finalActionSequence;
}

[Serializable]
public sealed class BattleCommandPostprocessDebugDto
{
    // 후처리 내부 판단을 로그로 확인하기 위한 preview 전용 DTO다.
    // dialog SLM input과 실행 dispatcher input으로 사용하지 않는다.
    public string parserThinking;
    public string[] droppedActorSummaries;
    public string[] adjustmentSummaries;
    public string[] refusalSummaries;
}

public sealed class BattleOrderLayerPreviewResult
{
    public SotParserRequestDto ParserRequest { get; }
    public string ParserRequestJson { get; }
    public SotDialogLayerRequestDto DialogRequest { get; }
    public string DialogRequestJson { get; }

    public SotParserOutputDto MockParserOutput { get; }
    public string MockParserOutputJson { get; }
    public BattleCommandPostprocessResult PostprocessResult { get; }
    public string PostprocessResultJson { get; }
    public SotDialogLayerResponseDto DialogResponse { get; }
    public string DialogResponseJson { get; }
    public string MockParserLog { get; }

    public BattleOrderLayerPreviewResult(
        SotParserRequestDto parserRequest,
        string parserRequestJson,
        SotParserOutputDto mockParserOutput,
        string mockParserOutputJson,
        BattleCommandPostprocessResult postprocessResult,
        string postprocessResultJson,
        SotDialogLayerRequestDto dialogRequest,
        string dialogRequestJson,
        SotDialogLayerResponseDto dialogResponse,
        string dialogResponseJson,
        string mockParserLog
    )
    {
        ParserRequest = parserRequest;
        ParserRequestJson = parserRequestJson;
        MockParserOutput = mockParserOutput;
        MockParserOutputJson = mockParserOutputJson;
        PostprocessResult = postprocessResult;
        PostprocessResultJson = postprocessResultJson;
        DialogRequest = dialogRequest;
        DialogRequestJson = dialogRequestJson;
        DialogResponse = dialogResponse;
        DialogResponseJson = dialogResponseJson;
        MockParserLog = mockParserLog;
    }
}
