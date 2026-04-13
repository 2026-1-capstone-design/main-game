using System;

[Serializable]
public sealed class BattleLlmResponseDto
{
    public BattleLlmResponseOutputDto output;
}

[Serializable]
public sealed class BattleLlmResponseOutputDto
{
    public string thinking;
    public string dialog;
    public string intent;
    public string skillUsagePolicy;
    public string positioning;
    public string targetUnitId;

    // Legacy field kept for compatibility with older logs/responses.
    public BattleLlmResponseActionDto[] action;
}

[Serializable]
public sealed class BattleLlmResponseActionDto
{
    public string type;
    public string subtype;

    // attack.target = "E_01"
    public string targetUnitId;

    // move.absolute.target = [x, y]
    public BattleLlmVector2Dto absoluteTarget;

    // move.relative.from = "A_02"
    public string relativeFromUnitId;

    // move.relative.offset = [x, y]
    public BattleLlmVector2Dto relativeOffset;
}
