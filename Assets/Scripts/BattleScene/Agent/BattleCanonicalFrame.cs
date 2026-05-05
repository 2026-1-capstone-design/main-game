using UnityEngine;

public static class BattleCanonicalFrame
{
    // Self-play uses one shared policy for both teams, so the hostile team observes
    // and acts in a 180-degree rotated frame to preserve left/right symmetry.
    public static Vector2 ToCanonical(BattleTeamId teamId, Vector2 vector) =>
        teamId == BattleTeamIds.Enemy ? -vector : vector;

    public static Vector3 ToCanonical(BattleTeamId teamId, Vector3 vector) =>
        teamId == BattleTeamIds.Enemy ? -vector : vector;

    public static Vector2 ToWorld(BattleTeamId teamId, Vector2 vector) =>
        teamId == BattleTeamIds.Enemy ? -vector : vector;
}
