// ============================================================
//  TacticalIntent.cs
//  Assets/Scripts/BattleScene/ 에 넣으세요.
//
//  SLM이 뱉을 execute_tactical_intent 응답을 게임 내부에서
//  표현하는 데이터 구조입니다.
//  1단계에서는 하드코딩으로 이 구조를 직접 만들어서
//  IntentExecutor에 주입합니다.
// ============================================================

/// <summary>
/// 전술적 목표. SLM의 "intent" 필드와 1:1 대응.
/// </summary>
public enum TacticalIntentType
{
    None = 0,
    Assassinate = 1,    // 단일 적 집중 제거
    Support = 2,    // 아군 보호·힐
    Vanguard = 3,    // 전방 돌파
    HoldLine = 4,    // 현 위치 사수
    Retreat = 5,    // 교전 이탈 및 후퇴
    Kite = 6,    // 사거리 유지 견제
    Regroup = 7,    // 아군 대열 합류/재집결
}

/// <summary>
/// 스킬 사용 정책. SLM의 "skill_usage_policy" 필드와 1:1 대응.
/// </summary>
public enum SkillUsagePolicy
{
    OnCooldown = 0,    // 쿨 되면 바로 사용
    SaveForCritical = 1,    // HP 임계 상황에서만 사용
    Initiative = 2,    // 선제적으로 사용
    Reactive = 3,    // 피격 시 반응적으로 사용
}

/// <summary>
/// 위치 선정 스타일. SLM의 "positioning" 필드와 1:1 대응.
/// </summary>
public enum PositioningStyle
{
    KeepMaxRange = 0,    // 최대 사거리 유지
    CloseQuarter = 1,    // 근접 밀착
    Flanking = 2,    // 측면 우회
}

/// <summary>
/// SLM의 execute_tactical_intent 응답 전체를 담는 구조체.
/// BattleRuntimeUnit에 붙어서 한 유닛의 현재 의도를 표현합니다.
/// </summary>
public sealed class TacticalIntent
{
    public TacticalIntentType Intent { get; }
    public SkillUsagePolicy SkillPolicy { get; }
    public PositioningStyle Positioning { get; }

    /// <summary>주 타겟 unit (적). null이면 의도 실행기가 자동 선택.</summary>
    public BattleRuntimeUnit PrimaryTarget { get; }

    /// <summary>보조 타겟 unit (아군 힐·버프용). null 허용.</summary>
    public BattleRuntimeUnit SecondaryTarget { get; }

    // ── 이벤트 기반 재평가를 위한 메타 정보 ──────────────
    /// <summary>이 의도가 생성된 시점의 타겟 체력 (타겟 사망 감지용).</summary>
    public bool WasPrimaryTargetAliveOnCreate { get; }

    public TacticalIntent(
        TacticalIntentType intent,
        SkillUsagePolicy skillPolicy,
        PositioningStyle positioning,
        BattleRuntimeUnit primaryTarget,
        BattleRuntimeUnit secondaryTarget = null)
    {
        Intent = intent;
        SkillPolicy = skillPolicy;
        Positioning = positioning;
        PrimaryTarget = primaryTarget;
        SecondaryTarget = secondaryTarget;

        WasPrimaryTargetAliveOnCreate = primaryTarget != null && !primaryTarget.IsCombatDisabled;
    }

    /// <summary>
    /// 이 의도가 무효화되어 SLM 재호출이 필요한지 판단.
    /// 이벤트 기반 재평가의 핵심 조건들.
    /// </summary>
    public bool NeedsReEvaluation(BattleRuntimeUnit owner)
    {
        // 1. 주 타겟이 사망한 경우
        if (WasPrimaryTargetAliveOnCreate &&
            (PrimaryTarget == null || PrimaryTarget.IsCombatDisabled))
            return true;

        // 2. 자기 자신이 전투 불능인 경우
        if (owner == null || owner.IsCombatDisabled)
            return true;

        return false;
    }

    public override string ToString()
    {
        string targetName = PrimaryTarget != null ? PrimaryTarget.DisplayName : "None";
        return $"Intent={Intent}, Skill={SkillPolicy}, Pos={Positioning}, Target={targetName}";
    }
}
