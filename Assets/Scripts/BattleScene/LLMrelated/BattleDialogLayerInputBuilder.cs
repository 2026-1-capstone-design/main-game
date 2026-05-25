// 대사 레이어에 넘길 입력을 만든다.
// 대사 레이어는 finalActionSequence를 수정하지 않는다.

using System.Collections.Generic;

public sealed class BattleDialogLayerInputBuilder
{
    public SotDialogLayerRequestDto BuildDummy(string originalCommand, BattleOrderRuntimeContext context)
    {
        List<SotDialogActorInputDto> actors = new List<SotDialogActorInputDto>();

        BattleRuntimeUnit actor = FindFirstAliveAlly(context != null ? context.Allies : null);
        if (actor != null)
        {
            actors.Add(
                new SotDialogActorInputDto
                {
                    unitId = BattleOrderRuntimeQueries.GetUnitId(actor, context.RosterProjection),
                    speechStyle = ResolveSpeechStyle(actor),
                    personalityDescription = ResolvePersonalityDescription(actor),
                    sourceDialog = "명령을 확인했다.",
                    obedienceState = "obey",
                    obeyedActionAdjustment = string.Empty,
                    refusalSummary = string.Empty,
                    finalActionSequence = new[]
                    {
                        new SotFinalActionDto
                        {
                            type = "wait",
                            durationSec = 1f,
                        },
                    },
                }
            );
        }

        return new SotDialogLayerRequestDto
        {
            originalCommand = originalCommand ?? string.Empty,
            actors = actors.ToArray(),
        };
    }

    private static BattleRuntimeUnit FindFirstAliveAlly(BattleRuntimeUnit[] allies)
    {
        if (allies == null)
            return null;

        for (int i = 0; i < allies.Length; i++)
        {
            BattleRuntimeUnit unit = allies[i];
            if (BattleOrderRuntimeQueries.IsAlive(unit))
                return unit;
        }

        return null;
    }

    private static string ResolvePersonalityDescription(BattleRuntimeUnit unit)
    {
        if (unit == null || unit.Snapshot == null || unit.Snapshot.Personality == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(unit.Snapshot.Personality.description)
            ? string.Empty
            : unit.Snapshot.Personality.description.Trim();
    }

    private static int ResolveSpeechStyle(BattleRuntimeUnit unit)
    {
        if (unit == null || unit.Snapshot == null || unit.Snapshot.Personality == null)
            return 0;

        return unit.Snapshot.Personality.speechStyle;
    }
}
