using UnityEngine;

public class CheatWeapon : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private EquipmentFactory equipmentFactory;

    [SerializeField]
    private InventoryManager inventoryManager;

    [Header("Cheat Settings")]
    public WeaponType targetWeaponType = WeaponType.oneHand;

    [Tooltip("무기에 부여할 스킬 ID (해당 무기 타입과 호환되어야 함)")]
    public WeaponSkillId targetSkillId = WeaponSkillId.None;

    [Tooltip("무기의 레벨 (Day 기준)")]
    public int dayOrLevel = 1;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
    }

    private void Start()
    {
        ResolveDependencies();
    }

    [ContextMenu("지정 무기 인벤토리에 강제 추가 (Cheat)")]
    public void GiveCheatWeapon()
    {
        ResolveDependencies();

        if (equipmentFactory == null || inventoryManager == null)
        {
            Debug.LogError("[CheatWeapon] EquipmentFactory 또는 InventoryManager가 연결되지 않았습니다.", this);
            return;
        }

        if (targetWeaponType == WeaponType.None)
        {
            Debug.LogError("[CheatWeapon] WeaponType을 지정해주세요.", this);
            return;
        }

        // 1. EquipmentFactory를 통해 정확한 스펙의 무기 데이터 강제 생성
        OwnedWeaponData cheatWeapon = equipmentFactory.CreateWeaponPreviewFromSpec(
            targetWeaponType,
            targetSkillId,
            dayOrLevel
        );

        if (cheatWeapon == null)
        {
            Debug.LogError(
                "[CheatWeapon] 무기 생성 실패. (지정한 WeaponType과 WeaponSkillId가 호환되지 않을 수 있습니다)",
                this
            );
            return;
        }

        // 2. 생성된 무기를 인벤토리에 즉시 추가 (마켓 구매 시 사용하는 함수 재사용)
        bool isAdded = inventoryManager.AddPurchasedWeaponFromMarketPreview(cheatWeapon);

        if (isAdded)
        {
            Debug.Log(
                $"[CheatWeapon] '{cheatWeapon.DisplayName}' (Lv.{cheatWeapon.Level}) 무기를 인벤토리에 성공적으로 추가했습니다!",
                this
            );
        }
        else
        {
            Debug.LogError("[CheatWeapon] 무기를 인벤토리에 추가하는 데 실패했습니다.", this);
        }
    }

    private void ResolveDependencies()
    {
        if (equipmentFactory == null)
        {
            equipmentFactory = FindFirstObjectByType<EquipmentFactory>(FindObjectsInactive.Include);
        }

        if (inventoryManager == null)
        {
            inventoryManager =
                InventoryManager.Instance ?? FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
        }
    }
}
