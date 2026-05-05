using BattleTest;

public readonly struct TrainingBattlePayloadSettings
{
    public readonly BattleTestPresetSO Preset;
    public readonly bool UseCurriculumTeamSize;
    public readonly string TeamSizeEnvironmentParameter;
    public readonly int DefaultTeamSize;
    public readonly GladiatorClassSO[] RandomClassPool;
    public readonly WeaponSO[] RandomWeaponPool;
    public readonly int DefaultUnitLevel;
    public readonly float DefaultStatMultiplier;
    public readonly string AllyStatMultiplierEnvironmentParameter;
    public readonly string EnemyStatMultiplierEnvironmentParameter;
    public readonly float DefaultAllyStatMultiplier;
    public readonly float DefaultEnemyStatMultiplier;

    public TrainingBattlePayloadSettings(
        BattleTestPresetSO preset,
        bool useCurriculumTeamSize,
        string teamSizeEnvironmentParameter,
        int defaultTeamSize,
        GladiatorClassSO[] randomClassPool,
        WeaponSO[] randomWeaponPool,
        int defaultUnitLevel,
        float defaultStatMultiplier,
        string allyStatMultiplierEnvironmentParameter,
        string enemyStatMultiplierEnvironmentParameter,
        float defaultAllyStatMultiplier,
        float defaultEnemyStatMultiplier
    )
    {
        Preset = preset;
        UseCurriculumTeamSize = useCurriculumTeamSize;
        TeamSizeEnvironmentParameter = teamSizeEnvironmentParameter;
        DefaultTeamSize = defaultTeamSize;
        RandomClassPool = randomClassPool;
        RandomWeaponPool = randomWeaponPool;
        DefaultUnitLevel = defaultUnitLevel;
        DefaultStatMultiplier = defaultStatMultiplier;
        AllyStatMultiplierEnvironmentParameter = allyStatMultiplierEnvironmentParameter;
        EnemyStatMultiplierEnvironmentParameter = enemyStatMultiplierEnvironmentParameter;
        DefaultAllyStatMultiplier = defaultAllyStatMultiplier;
        DefaultEnemyStatMultiplier = defaultEnemyStatMultiplier;
    }
}
