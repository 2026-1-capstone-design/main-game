// 전투 시작 시점의 BattleRuntimeUnit 이름과 SOT unitId 매핑을 보관한다.
// 유저 입력의 실제 유닛 이름을 A_01/E_01 형식으로 바꾼다.
// 유저 입력의 유닛 이름에 작은 오타가 있으면 현재 전투 명단 기준으로 보정한다.
// 대사 레이어 출력 text 안의 A_01/E_01 토큰을 실제 유닛 이름으로 바꾼다.
// 비정상 unitId 토큰이 섞인 대사는 fallback 문장으로 대체함. 안 들린다는 컨셉.

/*
오타 내성을 위한 수정안
1. exact match를 먼저 수행한다.
2. exact match 후 남은 토큰만 fuzzy match한다.
3. 현재 전투에 등장한 12명의 DisplayName만 후보로 둔다.
4. 편집거리 기준:
   - 이름 길이 2~4: distance 1까지만 허용
   - 이름 길이 5 이상: distance 2까지 허용
5. 같은 거리의 후보가 2개 이상이면 보정하지 않는다.
6. 명령어 토큰까지 억지로 이름으로 바꾸지 않도록 threshold를 낮게 유지한다.
*/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public sealed class BattleUnitNameResolver
{
    private const int MinimumApproximateNameLength = 2;

    private static readonly Regex SotTokenRegex = new Regex(
        @"(?<![A-Za-z0-9_])([AaEe])_([A-Za-z0-9]*)(?![A-Za-z0-9_])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly Regex UserNameTokenRegex = new Regex(
        @"(?<![A-Za-z0-9_])[\p{L}\p{N}]+(?![A-Za-z0-9_])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly string[] InvalidDialogFallbackTexts =
    {
        "(알아들을 수 없음)",
        "(전장의 소음에 묻혀 들리지 않음)",
    };

    private readonly Dictionary<string, BattleRuntimeUnit> _unitBySotId = new Dictionary<string, BattleRuntimeUnit>(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly Dictionary<BattleRuntimeUnit, string> _sotIdByUnit = new Dictionary<BattleRuntimeUnit, string>();

    private readonly Dictionary<string, string> _displayNameBySotId = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly Dictionary<string, string> _sotIdByDisplayName = new Dictionary<string, string>(
        StringComparer.Ordinal
    );

    private readonly List<KeyValuePair<string, string>> _nameReplacementPairs =
        new List<KeyValuePair<string, string>>();

    private readonly List<NameReplacementCandidate> _approximateNameCandidates = new List<NameReplacementCandidate>();

    private int _fallbackCursor;

    public void Clear()
    {
        _unitBySotId.Clear();
        _sotIdByUnit.Clear();
        _displayNameBySotId.Clear();
        _sotIdByDisplayName.Clear();
        _nameReplacementPairs.Clear();
        _approximateNameCandidates.Clear();
        _fallbackCursor = 0;
    }

    // 현재 전투의 ally/enemy runtime unit에서 SOT unitId와 실제 이름 매핑을 한 번 확정한다.
    public void Rebuild(
        IReadOnlyList<BattleRuntimeUnit> allyUnits,
        IReadOnlyList<BattleRuntimeUnit> enemyUnits,
        IBattleRosterProjection rosterProjection
    )
    {
        Clear();

        RegisterUnits(allyUnits, rosterProjection);
        RegisterUnits(enemyUnits, rosterProjection);

        _nameReplacementPairs.Sort(
            (left, right) =>
            {
                int lengthCompare = right.Key.Length.CompareTo(left.Key.Length);
                if (lengthCompare != 0)
                    return lengthCompare;

                return string.CompareOrdinal(left.Key, right.Key);
            }
        );

        _approximateNameCandidates.Sort(
            (left, right) =>
            {
                int lengthCompare = right.NormalizedDisplayName.Length.CompareTo(left.NormalizedDisplayName.Length);
                if (lengthCompare != 0)
                    return lengthCompare;

                return string.CompareOrdinal(left.DisplayName, right.DisplayName);
            }
        );
    }

    public bool TryGetSotId(BattleRuntimeUnit unit, out string sotId)
    {
        sotId = null;
        return unit != null && _sotIdByUnit.TryGetValue(unit, out sotId);
    }

    public void AppendDisplayNames(List<string> displayNames)
    {
        if (displayNames == null)
            return;

        for (int i = 0; i < _nameReplacementPairs.Count; i++)
        {
            string displayName = _nameReplacementPairs[i].Key;
            if (string.IsNullOrWhiteSpace(displayName))
                continue;

            if (!displayNames.Contains(displayName))
            {
                displayNames.Add(displayName);
            }
        }
    }

    public bool TryGetDisplayName(string sotId, out string displayName)
    {
        displayName = null;

        string canonicalSotId = NormalizeSotId(sotId);
        return canonicalSotId != null && _displayNameBySotId.TryGetValue(canonicalSotId, out displayName);
    }

    public BattleRuntimeUnit FindUnitBySotId(string sotId)
    {
        string canonicalSotId = NormalizeSotId(sotId);
        if (canonicalSotId == null)
            return null;

        return _unitBySotId.TryGetValue(canonicalSotId, out BattleRuntimeUnit unit) ? unit : null;
    }

    // 유저가 입력한 실제 유닛 이름을 SOT parser가 이해하는 A_01/E_01 문자열로 바꾼다.
    public string ReplaceDisplayNamesWithSotIds(string rawCommand)
    {
        if (string.IsNullOrWhiteSpace(rawCommand) || _nameReplacementPairs.Count == 0)
            return rawCommand ?? string.Empty;

        string result = rawCommand;

        for (int i = 0; i < _nameReplacementPairs.Count; i++)
        {
            string displayName = _nameReplacementPairs[i].Key;
            string sotId = _nameReplacementPairs[i].Value;

            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(sotId))
                continue;

            result = result.Replace(displayName, sotId);
        }

        return ReplaceApproximateDisplayNamesWithSotIds(result);
    }

    // 대사 레이어 결과 text 안의 SOT unitId를 실제 유닛 이름으로 바꾼다.
    // 비정상 unitId 조각이 있으면 원문을 버리고 fallback 대사를 반환한다.
    public bool TryResolveDialogText(string rawDialogText, out string resolvedDialogText, out string errorReason)
    {
        resolvedDialogText = string.Empty;
        errorReason = null;

        if (string.IsNullOrWhiteSpace(rawDialogText))
        {
            resolvedDialogText = NextInvalidDialogFallback();
            errorReason = "Dialog text is empty.";
            return false;
        }

        string source = rawDialogText.Trim();
        MatchCollection matches = SotTokenRegex.Matches(source);

        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            string matchedToken = match.Value;

            if (!TryNormalizeDialogSotToken(matchedToken, out string canonicalSotId))
            {
                resolvedDialogText = NextInvalidDialogFallback();
                errorReason = $"Invalid SOT unit id token '{matchedToken}'.";
                return false;
            }

            if (!_displayNameBySotId.ContainsKey(canonicalSotId))
            {
                resolvedDialogText = NextInvalidDialogFallback();
                errorReason = $"SOT unit id '{canonicalSotId}' is not present in current battle.";
                return false;
            }
        }

        resolvedDialogText = SotTokenRegex.Replace(
            source,
            match =>
            {
                if (!TryNormalizeDialogSotToken(match.Value, out string canonicalSotId))
                    return match.Value;

                return _displayNameBySotId.TryGetValue(canonicalSotId, out string displayName)
                    ? displayName
                    : match.Value;
            }
        );

        return true;
    }

    private void RegisterUnits(IReadOnlyList<BattleRuntimeUnit> units, IBattleRosterProjection rosterProjection)
    {
        if (units == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null)
                continue;

            string sotId = BuildSotId(unit, rosterProjection);
            string displayName = unit.DisplayName;

            if (string.IsNullOrWhiteSpace(sotId) || string.IsNullOrWhiteSpace(displayName))
                continue;

            string canonicalSotId = NormalizeSotId(sotId);
            if (canonicalSotId == null)
                continue;

            if (!_unitBySotId.ContainsKey(canonicalSotId))
                _unitBySotId.Add(canonicalSotId, unit);

            if (!_sotIdByUnit.ContainsKey(unit))
                _sotIdByUnit.Add(unit, canonicalSotId);

            if (!_displayNameBySotId.ContainsKey(canonicalSotId))
                _displayNameBySotId.Add(canonicalSotId, displayName);

            if (!_sotIdByDisplayName.ContainsKey(displayName))
            {
                _sotIdByDisplayName.Add(displayName, canonicalSotId);
                _nameReplacementPairs.Add(new KeyValuePair<string, string>(displayName, canonicalSotId));

                string normalizedDisplayName = NormalizeNameForMatching(displayName);
                if (!string.IsNullOrEmpty(normalizedDisplayName))
                {
                    _approximateNameCandidates.Add(
                        new NameReplacementCandidate(displayName, normalizedDisplayName, canonicalSotId)
                    );
                }
            }
        }
    }

    private string ReplaceApproximateDisplayNamesWithSotIds(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || _approximateNameCandidates.Count == 0)
            return source ?? string.Empty;

        return UserNameTokenRegex.Replace(
            source,
            match =>
            {
                string token = match.Value;
                if (string.IsNullOrWhiteSpace(token))
                    return token;

                if (TryResolveApproximateNameToken(token, out string sotId))
                    return sotId;

                return token;
            }
        );
    }

    private bool TryResolveApproximateNameToken(string rawToken, out string sotId)
    {
        sotId = null;

        string normalizedToken = NormalizeNameForMatching(rawToken);
        if (normalizedToken.Length < MinimumApproximateNameLength)
            return false;

        int bestDistance = int.MaxValue;
        string bestSotId = null;
        bool ambiguous = false;

        for (int i = 0; i < _approximateNameCandidates.Count; i++)
        {
            NameReplacementCandidate candidate = _approximateNameCandidates[i];
            int allowedDistance = CalculateAllowedNameDistance(
                normalizedToken.Length,
                candidate.NormalizedDisplayName.Length
            );

            if (allowedDistance <= 0)
                continue;

            if (Math.Abs(normalizedToken.Length - candidate.NormalizedDisplayName.Length) > allowedDistance)
                continue;

            int distance = CalculateEditDistanceBounded(
                normalizedToken,
                candidate.NormalizedDisplayName,
                allowedDistance
            );

            if (distance > allowedDistance)
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSotId = candidate.SotId;
                ambiguous = false;
            }
            else if (distance == bestDistance)
            {
                ambiguous = true;
            }
        }

        if (bestDistance == int.MaxValue || ambiguous || string.IsNullOrWhiteSpace(bestSotId))
            return false;

        sotId = bestSotId;
        return true;
    }

    private static int CalculateAllowedNameDistance(int tokenLength, int displayNameLength)
    {
        int shorterLength = Math.Min(tokenLength, displayNameLength);
        if (shorterLength < MinimumApproximateNameLength)
            return 0;

        int longerLength = Math.Max(tokenLength, displayNameLength);
        return longerLength >= 5 ? 2 : 1;
    }

    private static int CalculateEditDistanceBounded(string left, string right, int maxDistance)
    {
        if (left == null || right == null)
            return maxDistance + 1;

        if (Math.Abs(left.Length - right.Length) > maxDistance)
            return maxDistance + 1;

        if (left.Length == 0)
            return right.Length;

        if (right.Length == 0)
            return left.Length;

        int[] previous = new int[right.Length + 1];
        int[] current = new int[right.Length + 1];

        for (int j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            int rowMinimum = current[0];

            for (int j = 1; j <= right.Length; j++)
            {
                int substitutionCost = left[i - 1] == right[j - 1] ? 0 : 1;
                int deletion = previous[j] + 1;
                int insertion = current[j - 1] + 1;
                int substitution = previous[j - 1] + substitutionCost;
                int value = Math.Min(Math.Min(deletion, insertion), substitution);

                current[j] = value;
                if (value < rowMinimum)
                    rowMinimum = value;
            }

            if (rowMinimum > maxDistance)
                return maxDistance + 1;

            int[] temp = previous;
            previous = current;
            current = temp;
        }

        return previous[right.Length];
    }

    private static string NormalizeNameForMatching(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] buffer = new char[value.Length];
        int count = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (!char.IsLetterOrDigit(character))
                continue;

            buffer[count] = char.ToUpperInvariant(character);
            count++;
        }

        return count == 0 ? string.Empty : new string(buffer, 0, count);
    }

    private static string BuildSotId(BattleRuntimeUnit unit, IBattleRosterProjection rosterProjection)
    {
        if (unit == null)
            return null;

        if (rosterProjection != null)
            return rosterProjection.GetDisplayUnitId(unit);

        return $"U_{Math.Max(0, unit.UnitNumber):00}";
    }

    private static string NormalizeSotId(string rawUnitId)
    {
        if (string.IsNullOrWhiteSpace(rawUnitId))
            return null;

        string value = rawUnitId.Trim();
        Match match = SotTokenRegex.Match(value);
        if (!match.Success || match.Index != 0 || match.Length != value.Length)
            return null;

        if (!TryParseAllowedSotNumber(match.Groups[2].Value, out int unitNumber))
            return null;

        string prefix = match.Groups[1].Value.ToUpperInvariant();
        return $"{prefix}_{unitNumber:00}";
    }

    private static bool TryNormalizeDialogSotToken(string rawToken, out string canonicalSotId)
    {
        canonicalSotId = NormalizeSotId(rawToken);
        return canonicalSotId != null;
    }

    private static bool TryParseAllowedSotNumber(string rawNumber, out int unitNumber)
    {
        unitNumber = 0;

        if (string.IsNullOrWhiteSpace(rawNumber))
            return false;

        if (!int.TryParse(rawNumber, out int parsed))
            return false;

        if (parsed < 1 || parsed > BattleTeamConstants.MaxUnitsPerTeam)
            return false;

        unitNumber = parsed;
        return true;
    }

    private string NextInvalidDialogFallback()
    {
        if (InvalidDialogFallbackTexts.Length == 0)
            return "(알아들을 수 없음)";

        string fallback = InvalidDialogFallbackTexts[_fallbackCursor % InvalidDialogFallbackTexts.Length];
        _fallbackCursor++;
        return fallback;
    }

    private readonly struct NameReplacementCandidate
    {
        public readonly string DisplayName;
        public readonly string NormalizedDisplayName;
        public readonly string SotId;

        public NameReplacementCandidate(string displayName, string normalizedDisplayName, string sotId)
        {
            DisplayName = displayName ?? string.Empty;
            NormalizedDisplayName = normalizedDisplayName ?? string.Empty;
            SotId = sotId ?? string.Empty;
        }
    }
}
