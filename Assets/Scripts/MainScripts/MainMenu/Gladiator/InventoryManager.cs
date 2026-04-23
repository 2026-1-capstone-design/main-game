using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InventoryManager : SingletonBehaviour<InventoryManager>
{
    [SerializeField]
    private bool verboseLog = true;

    private readonly List<OwnedWeaponData> _ownedWeapons = new List<OwnedWeaponData>(); // 실제로 보유 중인 무기 목록

    private ContentDatabaseProvider _contentDatabaseProvider;
    private RandomManager _randomManager;

    private bool _initialized;
    private int _nextRuntimeId = 1; // 장비도 고유 런타임 ID

    public IReadOnlyList<OwnedWeaponData> OwnedWeapons => _ownedWeapons;

    protected override void Awake()
    {
        base.Awake();

        if (!IsPrimaryInstance)
        {
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    public void Initialize(ContentDatabaseProvider contentDatabaseProvider, RandomManager randomManager)
    {
        if (_initialized)
        {
            return;
        }

        _contentDatabaseProvider = contentDatabaseProvider;
        _randomManager = randomManager;

        if (_contentDatabaseProvider == null)
        {
            Debug.LogError("[InventoryManager] contentDatabaseProvider is null.", this);
            return;
        }

        if (_randomManager == null)
        {
            Debug.LogError("[InventoryManager] randomManager is null.", this);
            return;
        }

        _ownedWeapons.Clear();
        _nextRuntimeId = 1;

        _initialized = true;

        if (verboseLog)
        {
            Debug.Log("[InventoryManager] Initialized. Owned weapon contract ready.", this);
        }
    }

    public int GetOwnedWeaponCount()
    {
        return _ownedWeapons.Count;
    }

    public bool AddPurchasedWeaponFromMarketPreview(OwnedWeaponData marketPreview)
    {
        return TryAddOwnedWeaponFromPreview(marketPreview, out _);
    }

    // 무기 preview를 실제 보유 무기로 복사해 인벤토리에 추가한다.
    // 시장 구매 후 실제 무기 획득하는 경로
    public bool TryAddOwnedWeaponFromPreview(OwnedWeaponData weaponPreview, out OwnedWeaponData ownedWeapon)
    {
        ownedWeapon = null;

        if (!_initialized)
        {
            Debug.LogError("[InventoryManager] TryAddOwnedWeaponFromPreview called before Initialize.", this);
            return false;
        }

        if (weaponPreview == null)
        {
            Debug.LogError("[InventoryManager] weaponPreview is null.", this);
            return false;
        }

        if (weaponPreview.Weapon == null)
        {
            Debug.LogError("[InventoryManager] weaponPreview.Weapon is null.", this);
            return false;
        }

        ownedWeapon = CreateOwnedWeaponCopyFromPreview(weaponPreview);
        if (ownedWeapon == null)
        {
            Debug.LogError("[InventoryManager] Failed to copy weapon preview into owned weapon.", this);
            return false;
        }

        _ownedWeapons.Add(ownedWeapon);

        if (verboseLog)
        {
            string skillName = ownedWeapon.WeaponSkill != null ? ownedWeapon.WeaponSkill.skillName : "(None)";

            Debug.Log(
                $"[InventoryManager] Owned weapon added from preview. "
                    + $"Name={ownedWeapon.DisplayName}, Level={ownedWeapon.Level}, RuntimeId={ownedWeapon.RuntimeId}, Skill={skillName}",
                this
            );
        }

        return true;
    }

    // preview 무기의 레벨, 스킬, 최종 분산, 캐시 보너스를 그대로 복사하되,
    // !!새로운 RuntimeId를 가진!! owned 무기 인스턴스를 새로 만든다
    private OwnedWeaponData CreateOwnedWeaponCopyFromPreview(OwnedWeaponData weaponPreview)
    {
        if (weaponPreview == null || weaponPreview.Weapon == null)
        {
            return null;
        }

        OwnedWeaponData ownedWeapon = new OwnedWeaponData(
            _nextRuntimeId++,
            weaponPreview.DisplayName,
            weaponPreview.Level,
            weaponPreview.Weapon
        );

        ownedWeapon.WeaponSkill = weaponPreview.WeaponSkill;
        ownedWeapon.FinalAttackBonusVariancePercent = weaponPreview.FinalAttackBonusVariancePercent;
        ownedWeapon.FinalHealthBonusVariancePercent = weaponPreview.FinalHealthBonusVariancePercent;

        ownedWeapon.CachedAttackBonus = weaponPreview.CachedAttackBonus;
        ownedWeapon.CachedHealthBonus = weaponPreview.CachedHealthBonus;
        ownedWeapon.CachedAttackSpeedBonus = weaponPreview.CachedAttackSpeedBonus;
        ownedWeapon.CachedMoveSpeedBonus = weaponPreview.CachedMoveSpeedBonus;
        ownedWeapon.CachedAttackRangeBonus = weaponPreview.CachedAttackRangeBonus;

        return ownedWeapon;
    }

    // 실제 보유 무기를 인벤토리 목록에서 제거함.
    // 장착 중인지 여부는 여기서 확인하지 않고 외부에서 먼저 막는 시스템
    public bool RemoveOwnedWeapon(OwnedWeaponData weapon)
    {
        if (!_initialized)
        {
            Debug.LogError("[InventoryManager] RemoveOwnedWeapon called before Initialize.", this);
            return false;
        }

        if (weapon == null)
        {
            Debug.LogError("[InventoryManager] weapon is null.", this);
            return false;
        }

        bool removed = _ownedWeapons.Remove(weapon);
        if (!removed)
        {
            Debug.LogWarning($"[InventoryManager] RemoveOwnedWeapon failed. Name={weapon.DisplayName}", this);
            return false;
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[InventoryManager] Weapon removed. Name={weapon.DisplayName}, RuntimeId={weapon.RuntimeId}",
                this
            );
        }

        return true;
    }

    public void GrantRandomStarterWeapons(ContentDatabaseProvider contentDatabaseProvider)
    {
        if (!_initialized)
        {
            Debug.LogError("[InventoryManager] GrantRandomStarterWeapons called before Initialize.", this);
            return;
        }

        if (contentDatabaseProvider == null)
        {
            Debug.LogError("[InventoryManager] contentDatabaseProvider is null.", this);
            return;
        }

        if (_ownedWeapons.Count > 0)
        {
            if (verboseLog)
            {
                Debug.Log(
                    "[InventoryManager] Starter weapon grant skipped because owned weapon list is already populated.",
                    this
                );
            }

            return;
        }

        IReadOnlyList<WeaponSO> sourceWeapons = contentDatabaseProvider.Weapons;
        if (sourceWeapons == null || sourceWeapons.Count == 0)
        {
            Debug.LogError("[InventoryManager] No weapons found in ContentDatabaseProvider.", this);
            return;
        }

        List<WeaponSO> validWeapons = new List<WeaponSO>(sourceWeapons.Count);
        for (int i = 0; i < sourceWeapons.Count; i++)
        {
            WeaponSO weapon = sourceWeapons[i];
            if (weapon != null)
            {
                validWeapons.Add(weapon);
            }
        }

        if (validWeapons.Count < 2)
        {
            Debug.LogError("[InventoryManager] At least 2 valid weapons are required for starter grant.", this);
            return;
        }

        int firstIndex = _randomManager.NextInt(RandomStreamType.Equipment, 0, validWeapons.Count);
        int secondIndex = _randomManager.NextInt(RandomStreamType.Equipment, 0, validWeapons.Count - 1);
        if (secondIndex >= firstIndex)
        {
            secondIndex++;
        }

        OwnedWeaponData firstWeapon = CreateStarterWeapon(validWeapons[firstIndex]);
        OwnedWeaponData secondWeapon = CreateStarterWeapon(validWeapons[secondIndex]);

        if (firstWeapon == null || secondWeapon == null)
        {
            Debug.LogError("[InventoryManager] Failed to create starter weapons.", this);
            return;
        }

        _ownedWeapons.Add(firstWeapon);
        _ownedWeapons.Add(secondWeapon);

        if (verboseLog)
        {
            string firstSkill = firstWeapon.WeaponSkill != null ? firstWeapon.WeaponSkill.skillName : "(None)";
            string secondSkill = secondWeapon.WeaponSkill != null ? secondWeapon.WeaponSkill.skillName : "(None)";

            Debug.Log(
                $"[InventoryManager] Starter weapons granted. "
                    + $"Count={_ownedWeapons.Count}, "
                    + $"Weapon1={firstWeapon.DisplayName} ({firstSkill}), "
                    + $"Weapon2={secondWeapon.DisplayName} ({secondSkill})",
                this
            );
        }
    }

    private OwnedWeaponData CreateStarterWeapon(WeaponSO weapon)
    {
        if (weapon == null)
        {
            return null;
        }

        OwnedWeaponData ownedWeapon = new OwnedWeaponData(_nextRuntimeId++, weapon.weaponName, 1, weapon);

        ownedWeapon.WeaponSkill = PickRandomMatchingWeaponSkill(weapon.weaponType);
        ownedWeapon.FinalAttackBonusVariancePercent = 0f;
        ownedWeapon.FinalHealthBonusVariancePercent = 0f;

        RefreshDerivedStats(ownedWeapon);

        return ownedWeapon;
    }

    private void RefreshDerivedStats(OwnedWeaponData ownedWeapon)
    {
        if (ownedWeapon == null || ownedWeapon.Weapon == null)
        {
            return;
        }

        ownedWeapon.CachedAttackBonus = Mathf.Max(0f, ownedWeapon.Weapon.baseAttackBonus);
        ownedWeapon.CachedHealthBonus = Mathf.Max(0f, ownedWeapon.Weapon.baseHealthBonus);
        ownedWeapon.CachedAttackSpeedBonus = Mathf.Max(0f, ownedWeapon.Weapon.baseAttackSpeedBonus);
        ownedWeapon.CachedMoveSpeedBonus = Mathf.Max(0f, ownedWeapon.Weapon.baseMoveSpeedBonus);
        ownedWeapon.CachedAttackRangeBonus = Mathf.Max(0f, ownedWeapon.Weapon.baseAttackRangeBonus);
    }

    private WeaponSkillSO PickRandomMatchingWeaponSkill(WeaponType weaponType)
    {
        if (_contentDatabaseProvider == null)
        {
            return null;
        }

        IReadOnlyList<WeaponSkillSO> allSkills = _contentDatabaseProvider.WeaponSkills;
        if (allSkills == null || allSkills.Count == 0)
        {
            return null;
        }

        List<WeaponSkillSO> candidates = new List<WeaponSkillSO>(allSkills.Count);
        for (int i = 0; i < allSkills.Count; i++)
        {
            WeaponSkillSO skill = allSkills[i];
            if (skill == null)
            {
                continue;
            }

            if (skill.weaponType == weaponType)
            {
                candidates.Add(skill);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        int pickedIndex = _randomManager.NextInt(RandomStreamType.Equipment, 0, candidates.Count);
        return candidates[pickedIndex];
    }
}
