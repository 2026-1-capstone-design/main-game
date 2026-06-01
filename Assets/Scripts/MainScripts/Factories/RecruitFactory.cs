using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RecruitFactory : MonoBehaviour
{
    private const int MaxRandomGladiatorNameLength = 4;

    private static readonly string[] GladiatorNamePool =
    {
        "마커스",
        "세드릭",
        "휴고",
        "재스퍼",
        "브루노",
        "트리스탄",
        "데이먼",
        "펠릭스",
        "쿠엔틴",
        "말릭",
        "로넌",
        "개빈",
        "토바이어스",
        "레온",
        "헥터",
        "줄리언",
        "오스카",
        "라파엘",
        "도미닉",
        "이드리스",
        "켈런",
        "매그너스",
        "레미",
        "다리우스",
        "베넷",
        "자비에르",
        "사일러스",
        "콘래드",
        "티아고",
        "제인",
        "에버렛",
        "코맥",
        "마테오",
        "헨릭",
        "본",
        "기디언",
        "앤설",
        "트레버",
        "웨슬리",
        "파리스",
        "파스칼",
        "오리온",
        "타릭",
        "리암",
        "알든",
        "데클런",
        "롤런드",
        "스테판",
        "알라릭",
        "캘럼",
        "에밀",
        "파비안",
        "호아킨",
        "키건",
        "리앤더",
        "니콜라이",
        "오티스",
        "페리",
        "로더릭",
        "스텔란",
        "울릭",
        "밴스",
        "워런",
        "유수프",
        "졸탄",
        "에이브럼",
        "캐스피언",
        "에드거",
        "플로리안",
        "제리코",
        "칼리드",
        "로슨",
        "메릭",
        "나이얼",
        "오렌",
        "프레스턴",
        "라스무스",
        "설리번",
        "태런",
        "움베르토",
        "윌프레드",
        "요릭",
        "안톤",
        "바실",
        "코빈",
        "드미트리",
        "프랑코",
        "그레이디",
        "브램",
        "타데오",
        "에즈라",
        "루퍼트",
        "가레스",
        "그리핀",
        "달턴",
        "데스먼드",
        "도리안",
        "루카스",
        "밀로",
        "바스티안",
        "브록",
        "스펜서",
        "알프레드",
        "웬델",
        "유진",
        "카이우스",
        "커티스",
        "콜턴",
        "클라이브",
        "트로이",
        "퍼거스",
        "퍼시벌",
        "하워드",
        "호레이스",
        "가웨인",
        "세바스찬",
        "레녹스",
        "마빈",
        "모건",
        "챈들러",
        "아이리스",
        "나오미",
        "비앙카",
        "셀린",
        "프레야",
        "나디아",
        "비비안",
        "탈리아",
        "카르멘",
        "세레나",
        "다프네",
        "잉그리드",
        "마리나",
        "록사나",
        "키라",
        "조애나",
        "이벳",
        "노엘",
        "사브리나",
        "릴리스",
        "헬레나",
        "미레이유",
        "오데사",
        "발레리",
        "지젤",
        "타마라",
        "엘로디",
        "파라",
        "이졸데",
        "페트라",
        "로웨나",
        "클라우디아",
        "네레아",
        "조라",
        "알리나",
        "마엘",
        "시몬",
        "테레사",
        "비올레타",
        "셀레스트",
        "다니카",
        "에스텔",
        "팔로마",
        "모니크",
        "자흐라",
        "유니스",
        "야라",
        "플로렌스",
        "다비나",
        "콜레트",
        "델핀",
        "에이라",
        "피오렐라",
        "아우로라",
        "이마니",
        "조슬린",
        "칼리스타",
        "마리솔",
        "네리사",
        "오필리아",
        "프리실라",
        "라모나",
        "태비사",
        "우마",
        "베리티",
        "히메나",
        "욜란다",
        "지니아",
        "아델라",
        "브리지트",
        "코랄리",
        "파우스티나",
        "제서민",
        "라비니아",
        "이사도라",
        "조제트",
        "오드리",
        "레티시아",
        "로레타",
        "사빈",
        "포샤",
        "로저먼드",
        "사미라",
        "시어도라",
        "발렌시아",
        "세라핀",
        "이자벨",
        "줄레이카",
        "마틸다",
        "베레니스",
        "카탈리나",
        "엘비라",
        "레나타",
        "오틸리",
        "카멜리아",
        "마고",
        "유도라",
        "아그네스",
        "카산드라",
        "기네비어",
        "펠리시티",
        "그웬돌린",
        "하이디",
        "필리파",
        "오거스타",
        "샬럿",
        "테간",
        "코델리아",
        "도로시",
        "에블린",
        "올리브",
        "페넬로페",
        "루실",
        "우르술라",
        "클레어",
        "헨리에타",
        "메이브",
        "헤이즐",
        "파이드라",
        "모이라",
        "그레첸",
        "브렌나",
    };

    private static readonly string[] ShortGladiatorNamePool = BuildShortGladiatorNamePool();

    // 자연어 명령은 검투사 이름을 식별자로 사용하므로, 같은 메인씬 세션에서 생성되는 이름을 예약해 중복을 피한다.
    private static readonly HashSet<string> ReservedGladiatorDisplayNames = new HashSet<string>();

    [SerializeField]
    private bool verboseLog = true;

    private ContentDatabaseProvider _contentDatabaseProvider;
    private SessionManager _sessionManager;
    private RandomManager _randomManager;
    private EquipmentFactory _equipmentFactory; // 적 프리뷰 생성 시 무기를 붙여야 되니까
    private BalanceSO _balance;
    private bool _initialized;

    public BalanceSO Balance => _balance;

    public void Initialize(
        ContentDatabaseProvider contentDatabaseProvider,
        SessionManager sessionManager,
        RandomManager randomManager,
        EquipmentFactory equipmentFactory
    )
    {
        if (_initialized)
        {
            return;
        }

        _contentDatabaseProvider = contentDatabaseProvider;
        _sessionManager = sessionManager;
        _randomManager = randomManager;
        _equipmentFactory = equipmentFactory;
        _balance = _contentDatabaseProvider != null ? _contentDatabaseProvider.Balance : null;

        if (_contentDatabaseProvider == null)
        {
            Debug.LogError("[RecruitFactory] contentDatabaseProvider is null.", this);
            return;
        }

        if (_sessionManager == null && verboseLog)
        {
            Debug.LogWarning(
                "[RecruitFactory] sessionManager is null. Market/session flows may be unavailable, but stat preview generation can continue.",
                this
            );
        }

        if (_randomManager == null)
        {
            Debug.LogError("[RecruitFactory] randomManager is null.", this);
            return;
        }

        if (_equipmentFactory == null)
        {
            Debug.LogError("[RecruitFactory] equipmentFactory is null.", this);
            return;
        }

        if (_balance == null)
        {
            Debug.LogError("[RecruitFactory] BalanceSO is null.", this);
            return;
        }

        _initialized = true;

        if (verboseLog)
        {
            int templateCount =
                _contentDatabaseProvider.GladiatorClasses != null ? _contentDatabaseProvider.GladiatorClasses.Count : 0;

            Debug.Log(
                $"[RecruitFactory] Initialized. "
                    + $"GladiatorTemplateCount={templateCount}, "
                    + $"TraitCount={_contentDatabaseProvider.Traits.Count}, "
                    + $"PersonalityCount={_contentDatabaseProvider.Personalities.Count},"
                    + $"EquipmentFactoryReady={(_equipmentFactory != null)}",
                this
            );
        }
    }

    // 날짜를 기준으로 해서 검투사 프리뷰를 만들고,
    // 시장 슬롯에 올릴 offer 객체로 감싼다
    public MarketGladiatorOffer CreateMarketGladiatorOffer(int currentDay, int slotIndex)
    {
        if (!_initialized)
        {
            Debug.LogError("[RecruitFactory] CreateMarketGladiatorOffer called before Initialize.", this);
            return null;
        }

        OwnedGladiatorData preview = CreatePreviewGladiatorForDay(
            currentDay,
            RandomStreamType.Recruit,
            useSessionNameCounter: true
        );

        if (preview == null)
        {
            Debug.LogError("[RecruitFactory] Failed to create market gladiator preview.", this);
            return null;
        }

        int price = CalculateGladiatorPrice(preview, currentDay, RandomStreamType.Recruit);
        MarketGladiatorOffer offer = new MarketGladiatorOffer(slotIndex, preview, price);

        if (verboseLog)
        {
            Debug.Log(
                $"[RecruitFactory] Market gladiator created. "
                    + $"Slot={slotIndex}, Name={preview.DisplayName}, "
                    + $"Level={preview.Level}, Personal={preview.Personality} Loyalty={preview.Loyalty}, Price={price}",
                this
            );
        }

        return offer;
    }

    // 하루치 전투 후보 전체를 생성한다.
    // 실제 하나의 난이도 당 적 팀 하나는 CreateBattleEncounterPreviewForDifficulty에서 구성함.
    // 이렇게 난이도별 적 팀 preview를 만들고, BattleManager가 이를 캐시해 사용함.
    public List<BattleEncounterPreview> CreateBattleEncounterPreviewsForDay(
        int currentDay,
        int encounterCount = 4,
        int unitsPerEncounter = 6
    )
    {
        List<BattleEncounterPreview> encounters = new List<BattleEncounterPreview>(encounterCount);

        if (!_initialized)
        {
            Debug.LogError("[RecruitFactory] CreateBattleEncounterPreviewsForDay called before Initialize.", this);
            return encounters;
        }

        int safeDay = Mathf.Max(1, currentDay);
        unitsPerEncounter = Mathf.Max(1, unitsPerEncounter);

        BattleEncounterDifficulty[] orderedDifficulties =
        {
            BattleEncounterDifficulty.VeryLow,
            BattleEncounterDifficulty.Low,
            BattleEncounterDifficulty.Medium,
            BattleEncounterDifficulty.High,
        };

        int buildCount = Mathf.Min(Mathf.Max(1, encounterCount), orderedDifficulties.Length);

        for (int encounterIndex = 0; encounterIndex < buildCount; encounterIndex++)
        {
            BattleEncounterDifficulty difficulty = orderedDifficulties[encounterIndex];

            BattleEncounterPreview encounter = CreateBattleEncounterPreviewForDifficulty(
                safeDay,
                encounterIndex,
                difficulty,
                unitsPerEncounter
            );

            if (encounter != null)
            {
                encounters.Add(encounter);
            }
        }

        if (verboseLog)
        {
            for (int i = 0; i < encounters.Count; i++)
            {
                BattleEncounterPreview encounter = encounters[i];
                Debug.Log(
                    $"[RecruitFactory] Battle encounter cached. "
                        + $"Index={encounter.EncounterIndex}, Difficulty={encounter.Difficulty}, "
                        + $"AvgLv={encounter.AverageLevel:0.0}, RewardPreview={encounter.PreviewRewardGold}",
                    this
                );
            }
        }

        return encounters;
    }

    // 특정 난이도의 적 팀 1개를 실제로 구성함.
    // 적 유닛 레벨 분배, 적 검투사 preview 생성, 랜덤 무기 장착, snapshot 변환까지 담당.
    private BattleEncounterPreview CreateBattleEncounterPreviewForDifficulty(
        int currentDay,
        int encounterIndex,
        BattleEncounterDifficulty difficulty,
        int unitsPerEncounter
    )
    {
        List<BattleUnitSnapshot> units = new List<BattleUnitSnapshot>(unitsPerEncounter);
        List<int> unitLevels = BuildEncounterUnitLevels(currentDay, difficulty, unitsPerEncounter);
        float totalLevel = 0f;

        for (int unitIndex = 0; unitIndex < unitLevels.Count; unitIndex++)
        {
            OwnedGladiatorData preview = CreatePreviewGladiatorAtLevel(
                unitLevels[unitIndex],
                RandomStreamType.BattleEncounter,
                useSessionNameCounter: false
            );

            if (preview == null)
            {
                Debug.LogError(
                    $"[RecruitFactory] Failed to create battle encounter preview. "
                        + $"EncounterIndex={encounterIndex}, Difficulty={difficulty}, UnitIndex={unitIndex}",
                    this
                );
                return null;
            }

            if (!TryEquipRandomWeaponForBattlePreview(preview, currentDay))
            {
                Debug.LogError(
                    $"[RecruitFactory] Failed to equip random weapon on battle preview. "
                        + $"EncounterIndex={encounterIndex}, Difficulty={difficulty}, UnitIndex={unitIndex}",
                    this
                );
                return null;
            }

            preview.DisplayName = PickRandomGladiatorDisplayName(RandomStreamType.BattleEncounter);

            BattleUnitSnapshot snapshot = BattleUnitSnapshot.FromOwnedGladiator(preview, BattleTeamIds.Enemy);
            if (snapshot == null)
            {
                Debug.LogError(
                    $"[RecruitFactory] Failed to convert battle preview to snapshot. "
                        + $"EncounterIndex={encounterIndex}, Difficulty={difficulty}, UnitIndex={unitIndex}",
                    this
                );
                return null;
            }

            units.Add(snapshot);
            totalLevel += snapshot.Level;
        }

        float averageLevel = units.Count > 0 ? totalLevel / units.Count : 0f;
        int previewRewardGold = CalculatePreviewRewardForDifficulty(currentDay, difficulty);

        IReadOnlyList<Vector2> randomEnemyPositions = BattleDeploymentPositionUtility.BuildRandomEnemyPositions(
            _randomManager,
            units.Count,
            RandomStreamType.BattleEncounter
        );
        IReadOnlyList<Vector2> enemyPositions = BattleDeploymentPositionUtility.AssignEnemyPositionsByAttackRange(
            randomEnemyPositions,
            units
        );
        return new BattleEncounterPreview(
            encounterIndex,
            units,
            averageLevel,
            previewRewardGold,
            difficulty,
            enemyPositions
        );
    }

    // 레벨 = DAY 규칙에 맞춰 적 팀 유닛 레벨을 고정한다.
    private List<int> BuildEncounterUnitLevels(
        int currentDay,
        BattleEncounterDifficulty difficulty,
        int unitsPerEncounter
    )
    {
        List<int> levels = new List<int>(unitsPerEncounter);

        if (unitsPerEncounter <= 0)
        {
            return levels;
        }

        int level = Mathf.Max(1, currentDay);

        for (int i = 0; i < unitsPerEncounter; i++)
        {
            levels.Add(level);
        }

        return levels;
    }

    private OwnedGladiatorData CreatePreviewGladiatorAtLevel(
        int fixedLevel,
        RandomStreamType streamType,
        bool useSessionNameCounter
    )
    {
        GladiatorClassSO gladiatorTemplate = _contentDatabaseProvider.GladiatorTemplate;
        TraitSO trait = PickRandomNonNull(_contentDatabaseProvider.Traits, streamType);
        PersonalitySO personality = PickRandomNonNull(_contentDatabaseProvider.Personalities, streamType);

        if (gladiatorTemplate == null)
        {
            Debug.LogError("[RecruitFactory] Exactly one valid GladiatorClassSO is required.", this);
            return null;
        }

        if (trait == null)
        {
            Debug.LogError("[RecruitFactory] Failed because TraitSO is null.", this);
            return null;
        }

        if (personality == null)
        {
            Debug.LogError("[RecruitFactory] Failed because PersonalitySO is null.", this);
            return null;
        }

        int level = Mathf.Max(1, fixedLevel);
        int loyalty = RollLoyaltyFromPersonality(personality, streamType);
        int upkeep = CalculateGladiatorUpkeep(fixedLevel);

        int[] randomIndicates = GladiatorSkinManager.Instance.GenerateRandomSkinIndicates();

        string displayName = PickRandomGladiatorDisplayName(streamType);

        OwnedGladiatorData preview = new OwnedGladiatorData(
            0,
            displayName,
            level,
            0,
            loyalty,
            upkeep,
            gladiatorTemplate,
            trait,
            personality,
            null,
            null,
            randomIndicates
        );

        preview.FinalHealthVariancePercent = _randomManager.NextFloatRange(
            streamType,
            _balance.gladiatorFinalStatVarianceMinPercent,
            _balance.gladiatorFinalStatVarianceMaxPercent
        );

        preview.FinalAttackVariancePercent = _randomManager.NextFloatRange(
            streamType,
            _balance.gladiatorFinalStatVarianceMinPercent,
            _balance.gladiatorFinalStatVarianceMaxPercent
        );

        RefreshDerivedStats(preview, fixedLevel, true);
        return preview;
    }

    // 날짜 기반 레벨의 검투사 preview를 생성함.
    // 시장애 진열되는 검투사와 일부 적 preview 생성의 시작점
    private OwnedGladiatorData CreatePreviewGladiatorForDay(
        int currentDay,
        RandomStreamType streamType,
        bool useSessionNameCounter
    )
    {
        GladiatorClassSO gladiatorTemplate = _contentDatabaseProvider.GladiatorTemplate;
        TraitSO trait = PickRandomNonNull(_contentDatabaseProvider.Traits, streamType);
        PersonalitySO personality = PickRandomNonNull(_contentDatabaseProvider.Personalities, streamType);

        if (gladiatorTemplate == null)
        {
            Debug.LogError("[RecruitFactory] Exactly one valid GladiatorClassSO is required.", this);
            return null;
        }

        if (trait == null)
        {
            Debug.LogError("[RecruitFactory] Failed because TraitSO is null.", this);
            return null;
        }

        if (personality == null)
        {
            Debug.LogError("[RecruitFactory] Failed because PersonalitySO is null.", this);
            return null;
        }

        int level = CalculateDayBasedLevel(currentDay, streamType);
        int loyalty = RollLoyaltyFromPersonality(personality, streamType);
        int upkeep = CalculateGladiatorUpkeep(currentDay);

        int[] randomIndicates = GladiatorSkinManager.Instance.GenerateRandomSkinIndicates();

        string displayName = PickRandomGladiatorDisplayName(streamType);

        OwnedGladiatorData preview = new OwnedGladiatorData(
            0,
            displayName,
            level,
            0,
            loyalty,
            upkeep,
            gladiatorTemplate,
            trait,
            personality,
            null,
            null,
            randomIndicates
        );

        preview.FinalHealthVariancePercent = _randomManager.NextFloatRange(
            streamType,
            _balance.gladiatorFinalStatVarianceMinPercent,
            _balance.gladiatorFinalStatVarianceMaxPercent
        );

        preview.FinalAttackVariancePercent = _randomManager.NextFloatRange(
            streamType,
            _balance.gladiatorFinalStatVarianceMinPercent,
            _balance.gladiatorFinalStatVarianceMaxPercent
        );

        RefreshDerivedStats(preview, currentDay, true);
        return preview;
    }

    private int CalculateDayBasedLevel(int currentDay, RandomStreamType streamType)
    {
        return Mathf.Max(1, currentDay);
    }

    private int CalculatePreviewRewardForDifficulty(int currentDay, BattleEncounterDifficulty difficulty)
    {
        return CalculateRewardForDifficulty(_balance, currentDay, difficulty);
    }

    public static int CalculateRewardForDifficulty(
        BalanceSO balance,
        int currentDay,
        BattleEncounterDifficulty difficulty
    )
    {
        int safeDay = Mathf.Max(1, currentDay);

        if (balance == null)
        {
            return safeDay * 100;
        }

        return difficulty switch
        {
            BattleEncounterDifficulty.VeryLow => Mathf.Max(
                0,
                balance.veryLowRewardBase + (balance.veryLowRewardPerLevel * safeDay)
            ),
            BattleEncounterDifficulty.Low => Mathf.Max(
                0,
                balance.lowRewardBase + (balance.lowRewardPerLevel * safeDay)
            ),
            BattleEncounterDifficulty.Medium => Mathf.Max(
                0,
                balance.mediumRewardBase + (balance.mediumRewardPerLevel * safeDay)
            ),
            BattleEncounterDifficulty.High => Mathf.Max(
                0,
                balance.highRewardBase + (balance.highRewardPerLevel * safeDay)
            ),
            _ => Mathf.Max(0, balance.mediumRewardBase + (balance.mediumRewardPerLevel * safeDay)),
        };
    }

    public int CalculateGladiatorPrice(OwnedGladiatorData gladiator, int currentDay, RandomStreamType streamType)
    {
        if (gladiator == null)
        {
            return 0;
        }

        int levelPriceMin = _balance != null ? _balance.gladiatorMarketPricePerLevelMin : 40;
        int levelPriceMax = _balance != null ? _balance.gladiatorMarketPricePerLevelMax : 60;
        if (levelPriceMax < levelPriceMin)
        {
            levelPriceMax = levelPriceMin;
        }

        int perLevelPrice =
            _randomManager != null
                ? _randomManager.NextInt(streamType, levelPriceMin, levelPriceMax + 1)
                : Mathf.RoundToInt((levelPriceMin + levelPriceMax) * 0.5f);

        int baseMarketPrice = _balance != null ? Mathf.Max(0, _balance.gladiatorBaseMarketPrice) : 2000;
        int dayPrice = Mathf.Max(0, perLevelPrice) * Mathf.Max(1, currentDay);
        int statDeltaPrice = CalculateGladiatorStatDeltaPrice(gladiator);

        return Mathf.Max(0, baseMarketPrice + dayPrice + statDeltaPrice);
    }

    public int CalculateGladiatorUpkeep(int level)
    {
        int baseUpkeep = _balance != null ? Mathf.Max(0, _balance.gladiatorBaseUpkeep) : 2000;
        int upkeepPerLevel = _balance != null ? Mathf.Max(0, _balance.gladiatorUpkeepPerLevel) : 100;
        return Mathf.Max(0, baseUpkeep + (Mathf.Max(1, level) * upkeepPerLevel));
    }

    private static int CalculateGladiatorStatDeltaPrice(OwnedGladiatorData gladiator)
    {
        if (gladiator == null || gladiator.GladiatorClass == null)
            return 0;

        var baseStat = gladiator.GladiatorClass;

        float additionalHealth = Mathf.Max(0f, gladiator.CachedMaxHealth - baseStat.baseHealth);
        float healthPremium = additionalHealth * 1f;

        float baseDps = baseStat.baseAttack * baseStat.attackSpeed;
        float currentDps = gladiator.CachedAttack * gladiator.CachedAttackSpeed;

        float offensivePremium = 0f;
        if (baseDps > 0.001f)
        {
            offensivePremium = (currentDps / baseDps) * baseStat.baseAttack * 50f;
        }

        return Mathf.RoundToInt(healthPremium + offensivePremium);
    }

    private int RollLoyaltyFromPersonality(PersonalitySO personality, RandomStreamType streamType)
    {
        int min = _balance != null ? _balance.loyaltyMin : 0;
        int max = _balance != null ? _balance.loyaltyMax : 100;

        if (max < min)
        {
            max = min;
        }

        int mean = personality != null ? personality.baseLoyalty : min;
        mean = Mathf.Clamp(mean, min, max);

        float sigma = mean / 3f;
        if (sigma <= 0f)
        {
            return mean;
        }

        float sampled = NextGaussian(mean, sigma, streamType);
        int rounded = Mathf.RoundToInt(sampled);
        return Mathf.Clamp(rounded, min, max);
    }

    private float NextGaussian(float mean, float standardDeviation, RandomStreamType streamType)
    {
        if (standardDeviation <= 0f)
        {
            return mean;
        }

        float u1 = Mathf.Clamp(_randomManager.NextFloatRange(streamType, 0.0001f, 1f), 0.0001f, 1f);
        float u2 = Mathf.Clamp01(_randomManager.NextFloatRange(streamType, 0f, 1f));

        float radius = Mathf.Sqrt(-2f * Mathf.Log(u1));
        float theta = 2f * Mathf.PI * u2;
        float standardNormal = radius * Mathf.Cos(theta);

        return mean + standardDeviation * standardNormal;
    }

    // 검투사 클래스 기본치 + 레벨 성장 + 최종 분산 + 장착 무기 보너스를 반영해
    // 전투용 캐시용 스탯을 계산함.
    // 프리뷰 단계에서도 이 함수로 실제 전투 진입 전 능력치가 확정됨.
    private void RefreshDerivedStats(OwnedGladiatorData gladiator, bool fullyHeal)
    {
        RefreshDerivedStats(gladiator, gladiator != null ? gladiator.Level : 0, fullyHeal);
    }

    private void RefreshDerivedStats(OwnedGladiatorData gladiator, int currentDay, bool fullyHeal)
    {
        if (gladiator == null)
        {
            Debug.LogError("[RecruitFactory] RefreshDerivedStats received null gladiator.", this);
            return;
        }

        if (gladiator.GladiatorClass == null)
        {
            Debug.LogError("[RecruitFactory] RefreshDerivedStats failed because GladiatorClass is null.", this);
            return;
        }

        float oldCurrentHealth = gladiator.CurrentHealth;
        float oldMaxHealth = gladiator.CachedMaxHealth;

        int dayOffset = Mathf.Max(0, currentDay - 1);

        float baseHealth = Mathf.Max(0f, gladiator.GladiatorClass.baseHealth);
        float healthGrowthPerLevel = Mathf.Max(0f, gladiator.GladiatorClass.healthGrowthPerLevel);
        float scaledHealth = baseHealth + (healthGrowthPerLevel * dayOffset);

        float baseAttack = Mathf.Max(0f, gladiator.GladiatorClass.baseAttack);
        float attackGrowthPerLevel = Mathf.Max(0f, gladiator.GladiatorClass.attackGrowthPerLevel);
        float scaledAttack = baseAttack + (attackGrowthPerLevel * dayOffset);

        float baseAttackSpeed = Mathf.Max(0f, gladiator.GladiatorClass.attackSpeed);
        float baseMoveSpeed = Mathf.Max(0f, gladiator.GladiatorClass.moveSpeed);
        float baseAttackRange = Mathf.Max(0f, gladiator.GladiatorClass.attackRange);

        float finalHealthMultiplier = 1f + gladiator.FinalHealthVariancePercent;
        float finalAttackMultiplier = 1f + gladiator.FinalAttackVariancePercent;

        if (finalHealthMultiplier < 0f)
        {
            finalHealthMultiplier = 0f;
        }

        if (finalAttackMultiplier < 0f)
        {
            finalAttackMultiplier = 0f;
        }

        float weaponHealthBonus = 0f;
        float weaponAttackBonus = 0f;
        float weaponAttackSpeedBonus = 0f;
        float weaponMoveSpeedBonus = 0f;
        float weaponAttackRangeBonus = 0f;

        if (gladiator.EquippedWeapon != null)
        {
            weaponHealthBonus = Mathf.Max(0f, gladiator.EquippedWeapon.CachedHealthBonus);
            weaponAttackBonus = Mathf.Max(0f, gladiator.EquippedWeapon.CachedAttackBonus);
            weaponAttackSpeedBonus = Mathf.Max(0f, gladiator.EquippedWeapon.CachedAttackSpeedBonus);
            weaponMoveSpeedBonus = Mathf.Max(0f, gladiator.EquippedWeapon.CachedMoveSpeedBonus);
            weaponAttackRangeBonus = Mathf.Max(0f, gladiator.EquippedWeapon.CachedAttackRangeBonus);
        }

        float newMaxHealth = (scaledHealth * finalHealthMultiplier) + weaponHealthBonus;
        float newAttack = (scaledAttack * finalAttackMultiplier) + weaponAttackBonus;
        float newAttackSpeed = baseAttackSpeed + weaponAttackSpeedBonus;
        float newMoveSpeed = baseMoveSpeed + weaponMoveSpeedBonus;
        float newAttackRange = baseAttackRange + weaponAttackRangeBonus;

        gladiator.CachedMaxHealth = Mathf.Max(0f, newMaxHealth);
        gladiator.CachedAttack = Mathf.Max(0f, newAttack);
        gladiator.CachedAttackSpeed = Mathf.Max(0f, newAttackSpeed);
        gladiator.CachedMoveSpeed = Mathf.Max(0f, newMoveSpeed);
        gladiator.CachedAttackRange = Mathf.Max(0f, newAttackRange);

        if (fullyHeal)
        {
            gladiator.CurrentHealth = gladiator.CachedMaxHealth;
        }
        else
        {
            if (gladiator.CachedMaxHealth > oldMaxHealth)
            {
                float gainedMaxHealth = gladiator.CachedMaxHealth - oldMaxHealth;
                gladiator.CurrentHealth = Mathf.Clamp(
                    oldCurrentHealth + gainedMaxHealth,
                    0f,
                    gladiator.CachedMaxHealth
                );
            }
            else
            {
                gladiator.CurrentHealth = Mathf.Clamp(oldCurrentHealth, 0f, gladiator.CachedMaxHealth);
            }
        }
    }

    private T PickRandomNonNull<T>(IReadOnlyList<T> list, RandomStreamType streamType)
        where T : class
    {
        if (list == null || list.Count == 0)
        {
            return null;
        }

        int validCount = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        int targetIndex = _randomManager.NextInt(streamType, 0, validCount);

        for (int i = 0; i < list.Count; i++)
        {
            T item = list[i];
            if (item == null)
            {
                continue;
            }

            if (targetIndex == 0)
            {
                return item;
            }

            targetIndex--;
        }

        return null;
    }

    // 적 검투사 preview에 랜덤 무기 preview를 장착하고,
    // 그 무기 보너스를 포함해 최종 스탯을 다시 계산해야함
    private bool TryEquipRandomWeaponForBattlePreview(OwnedGladiatorData preview, int currentDay)
    {
        if (preview == null)
        {
            return false;
        }

        if (_equipmentFactory == null)
        {
            Debug.LogError(
                "[RecruitFactory] TryEquipRandomWeaponForBattlePreview failed because equipmentFactory is null.",
                this
            );
            return false;
        }

        OwnedWeaponData weaponPreview = _equipmentFactory.CreateRandomWeaponPreviewForDay(currentDay);
        if (weaponPreview == null)
        {
            Debug.LogError("[RecruitFactory] Failed to create random weapon preview for battle enemy.", this);
            return false;
        }

        preview.EquippedWeapon = weaponPreview;
        RefreshDerivedStats(preview, currentDay, true);
        return true;
    }

    public static string PickRandomGladiatorDisplayName(RandomManager randomManager, RandomStreamType streamType)
    {
        string[] namePool = ShortGladiatorNamePool.Length > 0 ? ShortGladiatorNamePool : GladiatorNamePool;
        if (namePool.Length == 0)
        {
            return ReserveFallbackGladiatorDisplayName();
        }

        int startIndex =
            randomManager != null
                ? randomManager.NextInt(streamType, 0, namePool.Length)
                : Random.Range(0, namePool.Length);
        for (int offset = 0; offset < namePool.Length; offset++)
        {
            int index = (startIndex + offset) % namePool.Length;
            string candidate = namePool[index];
            if (TryReserveGladiatorDisplayName(candidate))
            {
                return candidate;
            }
        }

        return ReserveFallbackGladiatorDisplayName();
    }

    public static void ResetReservedGladiatorDisplayNames()
    {
        ReservedGladiatorDisplayNames.Clear();
    }

    public static void ReserveGladiatorDisplayName(string displayName)
    {
        TryReserveGladiatorDisplayName(displayName);
    }

    public static string ReserveOrCreateUniqueGladiatorDisplayName(
        string displayName,
        RandomManager randomManager,
        RandomStreamType streamType
    )
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            string trimmedName = displayName.Trim();
            if (TryReserveGladiatorDisplayName(trimmedName))
            {
                return trimmedName;
            }
        }

        return PickRandomGladiatorDisplayName(randomManager, streamType);
    }

    private static bool TryReserveGladiatorDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        return ReservedGladiatorDisplayNames.Add(displayName.Trim());
    }

    private static string ReserveFallbackGladiatorDisplayName()
    {
        int index = ReservedGladiatorDisplayNames.Count + 1;
        while (true)
        {
            string candidate = $"검투사{index}";
            if (TryReserveGladiatorDisplayName(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private string PickRandomGladiatorDisplayName(RandomStreamType streamType)
    {
        return PickRandomGladiatorDisplayName(_randomManager, streamType);
    }

    private static string[] BuildShortGladiatorNamePool()
    {
        List<string> names = new List<string>();
        for (int i = 0; i < GladiatorNamePool.Length; i++)
        {
            string name = GladiatorNamePool[i];
            if (!string.IsNullOrWhiteSpace(name) && name.Length <= MaxRandomGladiatorNameLength)
            {
                names.Add(name);
            }
        }

        return names.ToArray();
    }
}
