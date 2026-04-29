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

    public TrainingBattlePayloadSettings(
        BattleTestPresetSO preset,
        bool useCurriculumTeamSize,
        string teamSizeEnvironmentParameter,
        int defaultTeamSize,
        GladiatorClassSO[] randomClassPool,
        WeaponSO[] randomWeaponPool,
        int defaultUnitLevel,
        float defaultStatMultiplier
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
    }
}
