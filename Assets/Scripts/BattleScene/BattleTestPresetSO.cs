using System.Collections.Generic;
using UnityEngine;

namespace BattleTest
{
    [System.Serializable]
    public struct BattleTestUnitConfig
    {
        public GladiatorClassSO classSO;
        public int level;

        [Header("Weapon & Skill")]
        [Tooltip("WeaponSO 에셋을 드래그해서 넣으세요. 무기 모델과 애니메이션 타입이 자동으로 결정됩니다.")]
        public WeaponSO weaponData;
        public WeaponSkillId weaponSkillId;

        [Header("Stat Overrides (0 = Use Class Defaults)")]
        public float healthOverride;
        public float attackOverride;
        public float attackSpeedOverride;
        public float moveSpeedOverride;
        public float attackRangeOverride;

        [Header("Weapon Overrides")]
        public bool overrideWeaponSettings;
        public bool isRanged;
        public bool useProjectile;

        [Header("Multiplier")]
        [Tooltip("최종 스탯에 곱해지는 배율입니다. (0 또는 1 = 100%)")]
        public float statMultiplier;
    }

    [CreateAssetMenu(menuName = "Prototype/Test/Battle Test Preset", fileName = "NewBattleTestPreset")]
    public sealed class BattleTestPresetSO : ScriptableObject
    {
        public string scenarioName = "Test Scenario";

        [Header("Teams (Max 6 each)")]
        public List<BattleTestUnitConfig> allyTeam = new List<BattleTestUnitConfig>();
        public List<BattleTestUnitConfig> enemyTeam = new List<BattleTestUnitConfig>();

        [Header("Battle Rules")]
        public int battleSeed = 12345;
        public float enemyAverageLevel = 1f;
        public int previewRewardGold = 100;
    }
}
