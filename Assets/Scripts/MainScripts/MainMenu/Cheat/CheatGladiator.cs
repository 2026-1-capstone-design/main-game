using UnityEngine;

public class CheatGladiator : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GladiatorManager gladiatorManager;

    [Header("Cheat Settings")]
    [Tooltip("지정하지 않으면(비워두면) 데이터베이스에서 무작위 직업을 뽑아옵니다.")]
    public GladiatorClassSO targetClassSO;

    [Tooltip("지정하지 않으면(비워두면) 데이터베이스에서 무작위 성격을 뽑아옵니다.")]
    public PersonalitySO targetPersonalitySO;

    [Tooltip("검투사의 초기 레벨")]
    [Range(1, 50)]
    public int level = 1;

    [ContextMenu("검투사 강제 영입 (Cheat)")]
    public void GiveCheatGladiator()
    {
        if (gladiatorManager == null)
        {
            Debug.LogError("[CheatGladiator] GladiatorManager가 연결되지 않았습니다.", this);
            return;
        }

        // 1. 데이터베이스(ContentDatabaseProvider) 가져오기
        ContentDatabaseProvider db = ContentDatabaseProvider.Instance;
        if (db == null)
        {
            Debug.LogError("[CheatGladiator] ContentDatabaseProvider를 찾을 수 없습니다. (Main 씬에서 실행 중인지 확인하세요)", this);
            return;
        }

        // 2. 직업 선택 (인스펙터에 비워뒀을 경우 무작위 선택)
        GladiatorClassSO gladiatorClass = targetClassSO;
        if (gladiatorClass == null && db.GladiatorClasses != null && db.GladiatorClasses.Count > 0)
        {
            gladiatorClass = db.GladiatorClasses[Random.Range(0, db.GladiatorClasses.Count)];
        }

        if (gladiatorClass == null)
        {
            Debug.LogError("[CheatGladiator] 생성할 직업(GladiatorClassSO) 데이터가 없습니다.", this);
            return;
        }

        // 3. 성격 선택 (인스펙터에 비워뒀을 경우 무작위 선택)
        PersonalitySO personality = targetPersonalitySO;
        if (personality == null && db.Personalities != null && db.Personalities.Count > 0)
        {
            personality = db.Personalities[Random.Range(0, db.Personalities.Count)];
        }

        // 특성(Trait)은 기존처럼 무작위
        TraitSO trait = (db.Traits != null && db.Traits.Count > 0) ? db.Traits[Random.Range(0, db.Traits.Count)] : null;

        // 4. 외형 무작위 생성
        int[] skinIndices = GladiatorSkinManager.Instance != null ? GladiatorSkinManager.Instance.GenerateRandomSkinIndicates() : new int[12];

        // 5. 임시 데이터 설정
        string gladiatorName = $"치트 검투사 {Random.Range(100, 999)}";
        int loyalty = personality != null ? personality.baseLoyalty : 50;
        int upkeep = level * 100; // 임시 유지비

        // 시장(Market) 프리뷰 형태의 더미(Dummy) 데이터 생성
        OwnedGladiatorData dummyData = new OwnedGladiatorData(
            0, gladiatorName, level, 0, loyalty, upkeep,
            gladiatorClass, trait, personality, null, null, skinIndices
        );

        // 6. 스탯 수동 계산 (장비 미착용 기준, GladiatorManager의 계산식을 그대로 따름)
        int levelOffset = Mathf.Max(0, level - 1);
        float baseHealth = Mathf.Max(0f, gladiatorClass.baseHealth);
        float healthGrowth = Mathf.Max(0f, gladiatorClass.healthGrowthPerLevel);

        float baseAttack = Mathf.Max(0f, gladiatorClass.baseAttack);
        float attackGrowth = Mathf.Max(0f, gladiatorClass.attackGrowthPerLevel);

        dummyData.CachedMaxHealth = baseHealth + (healthGrowth * levelOffset);
        dummyData.CurrentHealth = dummyData.CachedMaxHealth;
        dummyData.CachedAttack = baseAttack + (attackGrowth * levelOffset);
        dummyData.CachedAttackSpeed = Mathf.Max(0f, gladiatorClass.attackSpeed);
        // 이속 2.0 기본값 보정
        dummyData.CachedMoveSpeed = gladiatorClass.moveSpeed > 0f ? gladiatorClass.moveSpeed : 2.0f;
        dummyData.CachedAttackRange = Mathf.Max(0f, gladiatorClass.attackRange);

        dummyData.FinalHealthVariancePercent = 0f;
        dummyData.FinalAttackVariancePercent = 0f;

        // 7. 시스템에 정식으로 영입 요청
        bool isAdded = gladiatorManager.AddPurchasedGladiatorFromMarketPreview(dummyData);

        if (isAdded)
        {
            string personalityName = personality != null ? personality.personalityName : "알 수 없음";
            Debug.Log($"[CheatGladiator] '{gladiatorName}' (직업: {gladiatorClass.name}, 성격: {personalityName}, Lv.{level}) 검투사를 영입했습니다!", this);
        }
        else
        {
            Debug.LogError("[CheatGladiator] 검투사 영입에 실패했습니다.", this);
        }
    }
}
