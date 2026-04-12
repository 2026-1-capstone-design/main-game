using System.Collections.Generic;
using UnityEngine;

public sealed class TrainingSpawnPlacementSampler
{
    public IReadOnlyDictionary<BattleTeamId, Vector3[]> GenerateRandomPlacements(
        BattleStartPayload payload,
        SphereCollider battlefieldCollider,
        float bodyRadius
    )
    {
        BattleTeamEntry playerTeam = payload.GetPlayerTeam();
        BattleTeamEntry hostileTeam = payload.GetHostileTeam();

        Vector3 center = battlefieldCollider != null ? battlefieldCollider.bounds.center : Vector3.zero;
        float radius =
            battlefieldCollider != null
                ? Mathf.Min(battlefieldCollider.bounds.extents.x, battlefieldCollider.bounds.extents.z) * 0.85f
                : 5f;
        float minSeparation = bodyRadius * 2f;

        float divAngle = Random.Range(0f, Mathf.PI * 2f);
        float nx = Mathf.Cos(divAngle);
        float nz = Mathf.Sin(divAngle);
        bool playerOnPositiveSide = Random.value > 0.5f;

        int playerCount = playerTeam.Units.Count;
        int hostileCount = hostileTeam.Units.Count;
        var placed = new List<Vector3>(playerCount + hostileCount);
        var playerPositions = new Vector3[playerCount];
        var hostilePositions = new Vector3[hostileCount];

        for (int i = 0; i < playerCount; i++)
        {
            playerPositions[i] = SampleHalfCircle(center, radius, nx, nz, playerOnPositiveSide, placed, minSeparation);
            placed.Add(playerPositions[i]);
        }

        for (int i = 0; i < hostileCount; i++)
        {
            hostilePositions[i] = SampleHalfCircle(
                center,
                radius,
                nx,
                nz,
                !playerOnPositiveSide,
                placed,
                minSeparation
            );
            placed.Add(hostilePositions[i]);
        }

        return new Dictionary<BattleTeamId, Vector3[]>
        {
            [playerTeam.TeamId] = playerPositions,
            [hostileTeam.TeamId] = hostilePositions,
        };
    }

    private static Vector3 SampleHalfCircle(
        Vector3 center,
        float radius,
        float nx,
        float nz,
        bool positiveSide,
        IList<Vector3> placed,
        float minSeparation
    )
    {
        for (int attempt = 0; attempt < 300; attempt++)
        {
            float x = Random.Range(-radius, radius);
            float z = Random.Range(-radius, radius);
            if (x * x + z * z > radius * radius)
            {
                continue;
            }

            float dot = x * nx + z * nz;
            if (positiveSide ? dot <= 0f : dot >= 0f)
            {
                continue;
            }

            Vector3 candidate = center + new Vector3(x, 0f, z);
            bool overlaps = false;
            for (int j = 0; j < placed.Count; j++)
            {
                if ((candidate - placed[j]).sqrMagnitude < minSeparation * minSeparation)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                return candidate;
            }
        }

        float sign = positiveSide ? 1f : -1f;
        return center + new Vector3(nx * radius * 0.4f * sign, 0f, nz * radius * 0.4f * sign);
    }
}
