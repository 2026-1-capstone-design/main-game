using UnityEngine;

// TrainingGladiatorPreset은 TrainingScene에서 학습 유닛의 최종 전투 스탯과 외형/무기 표현을
// 직접 고정하기 위한 ScriptableObject이다. 실제 BattleScene의 성장/장비 산식을 재계산하지 않는다.
[CreateAssetMenu(menuName = "Battle/Training/Gladiator Preset", fileName = "NewTrainingGladiatorPreset")]
public sealed class TrainingGladiatorPreset : ScriptableObject
{
    [Header("Identity")]
    public string displayNamePrefix = "Training";
    public int level = 1;
    public GladiatorClassSO gladiatorClass;

    [Header("Weapon Visuals")]
    public WeaponSO weapon;
    public bool overrideWeaponSettings;
    public bool isRanged;
    public bool useProjectile;

    [Header("Weapon Attack Animation Timing")]
    public bool overrideAttackAnimationTiming;
    public bool defaultDur;
    public float duration = 1f;

    [Tooltip("TrainingScene v1 keeps skills disabled in payloads.")]
    public WeaponSkillId weaponSkillId = WeaponSkillId.None;

    [Header("Final Stats")]
    public float maxHealth = 100f;
    public float attack = 10f;
    public float attackSpeed = 1f;
    public float moveSpeed = 3f;
    public float attackRange = 1f;

    [Header("Personality Bias")]
    [Range(0f, 1f)]
    public float personalityCollectivism = 0.5f;

    [Range(0f, 1f)]
    public float personalityPassiveness = 0.5f;

    [Header("Skin")]
    public int[] customizeIndicates = new int[(int)SkinPart.TotalCount];

    public bool IsValid => GetValidationError() == null;

    private void OnValidate()
    {
        level = Mathf.Max(1, level);
        maxHealth = Mathf.Max(0f, maxHealth);
        attack = Mathf.Max(0f, attack);
        attackSpeed = Mathf.Max(0f, attackSpeed);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        attackRange = Mathf.Max(0f, attackRange);
        duration = Mathf.Max(0f, duration);
        personalityCollectivism = Mathf.Clamp01(personalityCollectivism);
        personalityPassiveness = Mathf.Clamp01(personalityPassiveness);

        if (customizeIndicates == null || customizeIndicates.Length != (int)SkinPart.TotalCount)
        {
            int[] resized = new int[(int)SkinPart.TotalCount];
            if (customizeIndicates != null)
            {
                int copyCount = Mathf.Min(customizeIndicates.Length, resized.Length);
                for (int i = 0; i < copyCount; i++)
                {
                    resized[i] = customizeIndicates[i];
                }
            }

            customizeIndicates = resized;
        }
    }

    public string GetValidationError()
    {
        if (gladiatorClass == null)
            return "GladiatorClass is missing.";

        if (weapon == null)
            return "Weapon is missing.";

        if (customizeIndicates == null || customizeIndicates.Length != (int)SkinPart.TotalCount)
            return $"CustomizeIndicates must contain {(int)SkinPart.TotalCount} entries.";

        if (maxHealth <= 0f)
            return "MaxHealth must be greater than 0.";

        if (attack <= 0f)
            return "Attack must be greater than 0.";

        if (attackSpeed <= 0f)
            return "AttackSpeed must be greater than 0.";

        if (moveSpeed <= 0f)
            return "MoveSpeed must be greater than 0.";

        if (attackRange <= 0f)
            return "AttackRange must be greater than 0.";

        if (overrideAttackAnimationTiming && !defaultDur && duration <= 0f)
            return "Duration must be greater than 0 when custom attack animation timing is enabled.";

        return null;
    }

    public bool ResolveIsRanged() => overrideWeaponSettings ? isRanged : weapon != null && weapon.isRanged;

    public bool ResolveUseProjectile() =>
        overrideWeaponSettings ? useProjectile : weapon != null && weapon.useProjectile;

    public bool ResolveDefaultDur() => overrideAttackAnimationTiming ? defaultDur : weapon != null && weapon.defaultDur;

    public float ResolveDuration() =>
        overrideAttackAnimationTiming ? duration
        : weapon != null ? weapon.duration
        : 1f;

    public GladiatorPersonalityBias ResolvePersonalityBias() =>
        new GladiatorPersonalityBias(personalityCollectivism, personalityPassiveness);

    public int[] CloneCustomizeIndicates()
    {
        if (customizeIndicates == null)
            return null;

        int[] clone = new int[customizeIndicates.Length];
        for (int i = 0; i < customizeIndicates.Length; i++)
        {
            clone[i] = customizeIndicates[i];
        }

        return clone;
    }
}
