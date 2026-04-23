using System;

[Serializable]
public sealed class BattleLlmPromptDto
{
    public BattleLlmSystemPromptDto system_prompt;
    public BattleLlmUserInputDto user_input;
    public BattleLlmOutputTemplateDto output;
}

[Serializable]
public sealed class BattleLlmSystemPromptDto
{
    public string personality;
    public BattleLlmToolDto[] tools;
}

[Serializable]
public sealed class BattleLlmToolDto
{
    public string type;
    public string subtype;
    public string description;
    public BattleLlmToolParametersDto parameters;
}

[Serializable]
public sealed class BattleLlmToolParametersDto
{
    public string target;
    public string from;
    public string offset;
}

[Serializable]
public sealed class BattleLlmUserInputDto
{
    public BattleLlmAreaSituationDto area_situation;
    public string command;
}

[Serializable]
public sealed class BattleLlmAreaSituationDto
{
    public BattleLlmArenaDto arena;
    public BattleLlmUnitStateDto[] allies;
    public BattleLlmUnitStateDto[] enemies;
}

[Serializable]
public sealed class BattleLlmArenaDto
{
    public string shape;
    public BattleLlmVector2Dto center;
    public BattleLlmArenaSizeDto size;
}

[Serializable]
public sealed class BattleLlmArenaSizeDto
{
    public float width;
    public float height;
}

[Serializable]
public sealed class BattleLlmUnitStateDto
{
    public string unitId;
    public BattleLlmVector2Dto position;
    public BattleLlmStatsDto stats;
}

[Serializable]
public sealed class BattleLlmVector2Dto
{
    public float x;
    public float y;
}

[Serializable]
public sealed class BattleLlmStatsDto
{
    public float hp;
    public float atk;
    public float range;
}

[Serializable]
public sealed class BattleLlmOutputTemplateDto
{
    public string thinking;
    public string dialog;
    public BattleLlmOutputActionPlaceholderDto[] action;
}

[Serializable]
public sealed class BattleLlmOutputActionPlaceholderDto { }
