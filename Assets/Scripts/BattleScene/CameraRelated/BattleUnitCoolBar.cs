using UnityEngine;
using UnityEngine.UI;

public class BattleUnitCoolBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image AttackCoolBarFillImage; // 기본 공격 쿨다운 바[cite: 16]
    [SerializeField] private Image SkillCoolBarFillImage;  // 🌟 추가: 스킬 쿨다운 바

    private BattleUnitCombatState _targetState;
    private bool _isInitialized = false;

    // BattleRuntimeUnit이 초기화될 때 이 함수를 통해 State를 전달해 줍니다.[cite: 16]
    public void Setup(BattleUnitCombatState state)
    {
        _targetState = state;
        _isInitialized = true;
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
    }
}
