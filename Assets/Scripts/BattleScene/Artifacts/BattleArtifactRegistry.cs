using System;
using System.Collections.Generic;

// ArtifactId를 전투용 장신구 구현체 생성 함수로 연결한다.
// ScriptableObject 데이터와 순수 전투 로직 클래스를 분리하기 위한 레지스트리다.
public sealed class BattleArtifactRegistry
{
    private readonly Dictionary<ArtifactId, Func<IBattleArtifact>> _factories =
        new Dictionary<ArtifactId, Func<IBattleArtifact>>();

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
