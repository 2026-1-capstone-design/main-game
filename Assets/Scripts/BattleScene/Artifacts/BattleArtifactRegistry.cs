using System;
using System.Collections.Generic;

// ArtifactId를 전투용 장신구 구현체 생성 함수로 연결한다.
// ScriptableObject 데이터와 순수 전투 로직 클래스를 분리하기 위한 레지스트리다.
public sealed class BattleArtifactRegistry
{
    private readonly Dictionary<ArtifactId, Func<IBattleArtifact>> _factories =
        new Dictionary<ArtifactId, Func<IBattleArtifact>>();

    public BattleArtifactRegistry()
    {
        Register(ArtifactId.GreenAmber, () => new GreenAmberArtifact());
        Register(ArtifactId.OminousGaze, () => new OminousGazeArtifact());
        Register(ArtifactId.MaleficStarGaze, () => new MaleficStarGazeArtifact());
        Register(ArtifactId.TacticalMagnet, () => new TacticalMagnetArtifact());
        Register(ArtifactId.AssassinationManual, () => new AssassinationManualArtifact());
        Register(ArtifactId.AlphasFang, () => new AlphasFangArtifact());
        Register(ArtifactId.BrokenCrown, () => new BrokenCrownArtifact());
        Register(ArtifactId.MonstersClaw, () => new MonstersClawArtifact());
        Register(ArtifactId.VanguardCrest, () => new VanguardCrestArtifact());
        Register(ArtifactId.BreezeCloak, () => new BreezeCloakArtifact());
        Register(ArtifactId.GiantsHeaddress, () => new GiantsHeaddressArtifact());
        Register(ArtifactId.AssassinsDagger, () => new AssassinsDaggerArtifact());
        Register(ArtifactId.DeceiversMask, () => new DeceiversMaskArtifact());
        Register(ArtifactId.IronRosary, () => new IronRosaryArtifact());
        Register(ArtifactId.EagleQuill, () => new EagleQuillArtifact());
        Register(ArtifactId.BronzeRingOfTheDead, () => new BronzeRingOfTheDeadArtifact());
        Register(ArtifactId.AbyssalMedallion, () => new AbyssalMedallionArtifact());
        Register(ArtifactId.GiantSlayer, () => new GiantSlayerArtifact());
        Register(ArtifactId.MasterlessMedal, () => new MasterlessMedalArtifact());
        Register(ArtifactId.VampiricWeed, () => new VampiricWeedArtifact());
    }

    public void Register(ArtifactId artifactId, Func<IBattleArtifact> factory)
    {
        // None은 장착 없음 의미로 예약되어 실제 효과 등록 대상이 아니다.
        if (artifactId == ArtifactId.None || factory == null)
            return;

        _factories[artifactId] = factory;
    }

    public IBattleArtifact Create(ArtifactId artifactId) =>
        artifactId != ArtifactId.None && _factories.TryGetValue(artifactId, out Func<IBattleArtifact> factory)
            ? factory()
            : null;
}
