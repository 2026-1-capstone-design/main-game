using System.Collections.Generic;

// 전투 시작 시 유닛의 ArtifactId 목록을 전투용 효과 객체로 만들고 훅별로 캐싱한다.
// 매 피해 처리마다 전체 장신구를 다시 훑지 않도록 필요한 훅 목록만 유지한다.
public sealed class BattleArtifactSystem
{
    private readonly BattleArtifactRegistry _registry;
    private readonly List<ArtifactBinding<IDamageModifierArtifact>> _damageModifiers =
        new List<ArtifactBinding<IDamageModifierArtifact>>();
    private readonly List<ArtifactBinding<IBattleStartArtifactEffect>> _battleStartEffects =
        new List<ArtifactBinding<IBattleStartArtifactEffect>>();

    public BattleArtifactSystem(BattleArtifactRegistry registry = null)
    {
        _registry = registry ?? new BattleArtifactRegistry();
    }

    public void Initialize(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleFieldSnapshot snapshot,
        float battleTime,
        int battleTick,
        IBattleEffectSink effects
    )
    {
        // BattleSimulationManager.Initialize가 재호출될 수 있으므로 이전 전투의 훅 캐시를 비운다.
        _damageModifiers.Clear();
        _battleStartEffects.Clear();

        if (units == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit owner = units[i];
            BattleUnitCombatState ownerState = owner != null ? owner.State : null;
            if (owner == null || ownerState == null || owner.Snapshot == null || owner.Snapshot.ArtifactIds == null)
                continue;

            BattleEffectContext context = new BattleEffectContext(owner, null, snapshot, units, battleTime, battleTick);
            IReadOnlyList<ArtifactId> artifactIds = owner.Snapshot.ArtifactIds;
            for (int idIndex = 0; idIndex < artifactIds.Count; idIndex++)
            {
                // 아직 구현되지 않은 ID는 무시한다. 목록 플랜에서 개별 factory가 추가된다.
                IBattleArtifact artifact = _registry.Create(artifactIds[idIndex]);
                if (artifact == null)
                    continue;

                artifact.Initialize(ownerState, context);

                if (artifact is IDamageModifierArtifact damageModifier)
                    _damageModifiers.Add(
                        new ArtifactBinding<IDamageModifierArtifact>(ownerState, owner, damageModifier)
                    );

                if (artifact is IBattleStartArtifactEffect startEffect)
                    _battleStartEffects.Add(
                        new ArtifactBinding<IBattleStartArtifactEffect>(ownerState, owner, startEffect)
                    );
            }
        }

        for (int i = 0; i < _battleStartEffects.Count; i++)
        {
            ArtifactBinding<IBattleStartArtifactEffect> binding = _battleStartEffects[i];
            BattleEffectContext context = new BattleEffectContext(
                binding.OwnerView,
                null,
                snapshot,
                units,
                battleTime,
                battleTick
            );
            binding.Artifact.OnBattleStart(binding.Owner, context, effects);
        }
    }

    public void ModifyDamage(ref BattleDamageRequest request)
    {
        // 현재 단계에서는 모든 피해 보정 장신구가 요청을 볼 수 있다.
        // 소유자/대상 조건은 각 장신구 구현체가 request와 owner를 비교해 판단한다.
        for (int i = 0; i < _damageModifiers.Count; i++)
        {
            ArtifactBinding<IDamageModifierArtifact> binding = _damageModifiers[i];
            binding.Artifact.ModifyDamage(binding.Owner, ref request);
        }
    }

    // 훅 구현체와 그 소유 유닛을 함께 보관해 콜백마다 역조회하지 않게 한다.
    private readonly struct ArtifactBinding<T>
        where T : IBattleArtifact
    {
        public BattleUnitCombatState Owner { get; }
        public BattleRuntimeUnit OwnerView { get; }
        public T Artifact { get; }

        public ArtifactBinding(BattleUnitCombatState owner, BattleRuntimeUnit ownerView, T artifact)
        {
            Owner = owner;
            OwnerView = ownerView;
            Artifact = artifact;
        }
    }
}
