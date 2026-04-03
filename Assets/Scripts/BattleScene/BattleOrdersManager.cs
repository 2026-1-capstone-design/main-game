using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleOrdersManager : MonoBehaviour
{
    [SerializeField] private bool verboseLog = true;

    private readonly BattleRuntimeUnit[] _allyUnits = new BattleRuntimeUnit[6];
    private bool _initialized;

    public void Initialize(IReadOnlyList<BattleRuntimeUnit> runtimeUnits)
    {
        for (int i = 0; i < _allyUnits.Length; i++)
        {
            _allyUnits[i] = null;
        }

        if (runtimeUnits != null)
        {
            for (int i = 0; i < runtimeUnits.Count; i++)
            {
                BattleRuntimeUnit unit = runtimeUnits[i];
                if (unit == null)
                {
                    continue;
                }

                if (unit.IsEnemy)
                {
                    continue;
                }

                int allyIndex = unit.UnitNumber - 1;
                if (allyIndex < 0 || allyIndex >= _allyUnits.Length)
                {
                    continue;
                }

                _allyUnits[allyIndex] = unit;
            }
        }

        _initialized = true;

        if (verboseLog)
        {
            Debug.Log("[BattleOrdersManager] Initialized.", this);
        }
    }

    public void SubmitGlobalOrder(string rawOrderText)
    {
        if (!_initialized)
        {
            Debug.LogError("[BattleOrdersManager] SubmitGlobalOrder called before Initialize.", this);
            return;
        }

        StringBuilder sb = new StringBuilder(512);

        sb.AppendLine("<color=#4FC3F7><b>[GLOBAL]</b></color>");
        sb.AppendLine("<color=#B3E5FC>Global order received.</color>");

        for (int i = 0; i < _allyUnits.Length; i++)
        {
            sb.AppendLine(BuildGlobalAllyLine(i + 1, _allyUnits[i]));
        }

        sb.Append("<color=#FFCC80>Raw order:</color> \"");
        sb.Append(SanitizeRawText(rawOrderText));
        sb.Append('"');

        Debug.Log(sb.ToString(), this);
    }

    public void SubmitSingleOrder(BattleRuntimeUnit targetAlly, string rawOrderText)
    {
        if (!_initialized)
        {
            Debug.LogError("[BattleOrdersManager] SubmitSingleOrder called before Initialize.", this);
            return;
        }

        if (targetAlly == null)
        {
            Debug.LogWarning("[BattleOrdersManager] SubmitSingleOrder ignored. Target ally is null.", this);
            return;
        }

        if (targetAlly.IsEnemy)
        {
            Debug.LogWarning("[BattleOrdersManager] SubmitSingleOrder ignored. Target is an enemy unit.", this);
            return;
        }

        StringBuilder sb = new StringBuilder(256);

        sb.AppendLine("<color=#BA68C8><b>[SINGLE]</b></color>");
        sb.AppendLine("<color=#E1BEE7>Single target order received.</color>");
        sb.Append("<color=#81C784>Target ally:</color> ");
        sb.AppendLine(BuildUnitIdentityText(targetAlly));
        sb.Append("<color=#FFCC80>Raw order:</color> \"");
        sb.Append(SanitizeRawText(rawOrderText));
        sb.Append('"');

        Debug.Log(sb.ToString(), this);
    }

    private static string BuildGlobalAllyLine(int allyNumber, BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return $"<color=#AED581>Ally {allyNumber}:</color> Empty";
        }

        return $"<color=#AED581>Ally {allyNumber}:</color> {BuildUnitIdentityText(unit)}";
    }

    private static string BuildUnitIdentityText(BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return "Unknown, Loyalty=0, Personality=None";
        }

        string displayName = string.IsNullOrWhiteSpace(unit.DisplayName) ? "Unknown" : unit.DisplayName;
        int loyalty = unit.Snapshot != null ? unit.Snapshot.Loyalty : 0;

        string personalityName = "None";
        if (unit.Snapshot != null && unit.Snapshot.Personality != null)
        {
            personalityName = unit.Snapshot.Personality.name;
        }

        return $"{displayName}, Loyalty={loyalty}, Personality={personalityName}";
    }

    private static string SanitizeRawText(string rawOrderText)
    {
        if (string.IsNullOrEmpty(rawOrderText))
        {
            return string.Empty;
        }

        return rawOrderText.Replace("\r", " ").Replace("\n", " ");
    }
}