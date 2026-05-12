using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[System.Serializable]
public struct StatusIconData
{
    public BattleStatusType type;
    public Sprite iconSprite;
}

public class BattleUnitCoolBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image AttackCoolBarFillImage; // 기본 공격 쿨다운 바[cite: 16]
    [SerializeField] private Image SkillCoolBarFillImage;

    [Header("Status (Buff/Debuff) UI")]
    [SerializeField] private Transform statusContainer; // 아이콘들이 정렬될 부모 (Horizontal Layout Group 추천)
    [SerializeField] private BattleStatusIconUI statusIconPrefab; // 방금 만든 아이콘 프리팹
    [SerializeField] private int maxStatusIcons = 10; // 최대 표시할 버프 개수
    [SerializeField] private List<StatusIconData> statusIconDatabase; // 에디터에서 상태별 이미지 등록


    private BattleUnitCombatState _targetState;
    private bool _isInitialized = false;

    // UI 오브젝트 풀
    private List<BattleStatusIconUI> _statusIconPool = new List<BattleStatusIconUI>();
    private Dictionary<BattleStatusType, Sprite> _iconDict = new Dictionary<BattleStatusType, Sprite>();

    // BattleRuntimeUnit이 초기화될 때 이 함수를 통해 State를 전달해 줍니다.[cite: 16]
    public void Setup(BattleUnitCombatState state)
    {
        _targetState = state;
        _isInitialized = true;

        // 딕셔너리 세팅 (빠른 검색용)
        foreach (var data in statusIconDatabase)
        {
            _iconDict[data.type] = data.iconSprite;
        }

        // 아이콘 풀(Pool) 미리 생성
        for (int i = 0; i < maxStatusIcons; i++)
        {
            BattleStatusIconUI icon = Instantiate(statusIconPrefab, statusContainer);
            icon.Hide();
            _statusIconPool.Add(icon);
        }
    }

    private void Update()
    {
        if (!_isInitialized || _targetState == null)
            return;

        // 전투 불능 상태면 바를 모두 비워둡니다.
        if (_targetState.IsCombatDisabled)
        {
            if (AttackCoolBarFillImage != null) AttackCoolBarFillImage.fillAmount = 0f;
            if (SkillCoolBarFillImage != null) SkillCoolBarFillImage.fillAmount = 0f;
            foreach (var icon in _statusIconPool) icon.Hide();
            return;
        }

        // ── 1. 기본 공격 쿨다운 처리 ────────────────────────────────────────
        if (AttackCoolBarFillImage != null)
        {
            float maxAttackCooldown = _targetState.AttackSpeed > 0f ? 1f / _targetState.AttackSpeed : 0.01f;
            float currentAttackCooldown = _targetState.AttackCooldownRemaining;
            float attackFillRatio = 1f - Mathf.Clamp01(currentAttackCooldown / maxAttackCooldown);
            AttackCoolBarFillImage.fillAmount = attackFillRatio;
        }

        // ── 2. 스킬 쿨다운 처리 (새로 추가된 부분) ──────────────────────────
        if (SkillCoolBarFillImage != null)
        {
            // 스킬의 최대 쿨다운 시간 가져오기 (0으로 나누기 방지)
            float maxSkillCooldown = _targetState.SkillCooltime > 0f ? _targetState.SkillCooltime : 0.01f;

            // 남은 스킬 쿨다운 가져오기
            float currentSkillCooldown = _targetState.SkillCooldownRemaining;

            // 스킬 게이지 비율 계산 (0일 때 100%가 되도록)
            float skillFillRatio = 1f - Mathf.Clamp01(currentSkillCooldown / maxSkillCooldown);

            SkillCoolBarFillImage.fillAmount = skillFillRatio;
        }

        // ── 3. 상태 이상(버프/디버프) 처리 추가 ──────────────────────────
        UpdateStatusIcons();
    }


    private void UpdateStatusIcons()
    {
        var activeStatuses = _targetState.ActiveStatuses;

        int displayCount = Mathf.Min(activeStatuses.Count, maxStatusIcons);

        // 현재 유닛이 가진 버프 개수만큼 UI를 켜고 데이터를 업데이트합니다.
        for (int i = 0; i < displayCount; i++)
        {
            BattleStatusInstance status = activeStatuses[i];
            BattleStatusIconUI uiIcon = _statusIconPool[i];

            Sprite iconSprite = _iconDict.ContainsKey(status.Type) ? _iconDict[status.Type] : null;

            uiIcon.Setup(status, iconSprite);
            uiIcon.UpdateDuration(status.RemainingDuration);
        }

        // 남은 UI 풀은 숨깁니다.
        for (int i = displayCount; i < _statusIconPool.Count; i++)
        {
            _statusIconPool[i].Hide();
        }
    }
}
