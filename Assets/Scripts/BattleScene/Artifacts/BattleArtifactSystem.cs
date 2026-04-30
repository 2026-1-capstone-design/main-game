using System.Collections.Generic;

// 전투 시작 시 유닛의 ArtifactId 목록을 전투용 효과 객체로 만들고 훅별로 캐싱한다.
// 매 피해 처리마다 전체 장신구를 다시 훑지 않도록 필요한 훅 목록만 유지한다.
public sealed class BattleArtifactSystem : IBattleTargetingPolicy, IBattleMovementPolicy
{
    private readonly BattleArtifactRegistry _registry;
    private readonly List<ArtifactBinding<IDamageModifierArtifact>> _damageModifiers =
        new List<ArtifactBinding<IDamageModifierArtifact>>();
    private readonly List<ArtifactBinding<IBattleStartArtifactEffect>> _battleStartEffects =
        new List<ArtifactBinding<IBattleStartArtifactEffect>>();
    private readonly List<ArtifactBinding<ITargetingModifierArtifact>> _targetingModifiers =
        new List<ArtifactBinding<ITargetingModifierArtifact>>();
    private readonly List<ArtifactBinding<IMovementModifierArtifact>> _movementModifiers =
        new List<ArtifactBinding<IMovementModifierArtifact>>();
    private readonly List<ArtifactBinding<IDamageReactionArtifact>> _damageReactions =
        new List<ArtifactBinding<IDamageReactionArtifact>>();
    private readonly List<ArtifactBinding<IHealReactionArtifact>> _healReactions =
        new List<ArtifactBinding<IHealReactionArtifact>>();
    private readonly List<ArtifactBinding<IKillReactionArtifact>> _killReactions =
        new List<ArtifactBinding<IKillReactionArtifact>>();
    private readonly List<ArtifactBinding<ISkillCastReactionArtifact>> _skillCastReactions =
        new List<ArtifactBinding<ISkillCastReactionArtifact>>();
    private readonly List<ArtifactBinding<IAttackRetargetArtifact>> _attackRetargets =
        new List<ArtifactBinding<IAttackRetargetArtifact>>();
    private readonly List<ArtifactBinding<IPositionHistoryArtifact>> _positionHistoryEffects =
        new List<ArtifactBinding<IPositionHistoryArtifact>>();

    public IBattleTargetingPolicy TargetingPolicy => this;
    public IBattleMovementPolicy MovementPolicy => this;

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
        _targetingModifiers.Clear();
        _movementModifiers.Clear();
        _damageReactions.Clear();
        _healReactions.Clear();
        _killReactions.Clear();
        _skillCastReactions.Clear();
        _attackRetargets.Clear();
        _positionHistoryEffects.Clear();

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

                if (artifact is ITargetingModifierArtifact targetingModifier)
                    _targetingModifiers.Add(
                        new ArtifactBinding<ITargetingModifierArtifact>(ownerState, owner, targetingModifier)
                    );

                if (artifact is IMovementModifierArtifact movementModifier)
                    _movementModifiers.Add(
                        new ArtifactBinding<IMovementModifierArtifact>(ownerState, owner, movementModifier)
                    );

                if (artifact is IDamageReactionArtifact damageReaction)
                    _damageReactions.Add(
                        new ArtifactBinding<IDamageReactionArtifact>(ownerState, owner, damageReaction)
                    );

                if (artifact is IHealReactionArtifact healReaction)
                    _healReactions.Add(new ArtifactBinding<IHealReactionArtifact>(ownerState, owner, healReaction));

                if (artifact is IKillReactionArtifact killReaction)
                    _killReactions.Add(new ArtifactBinding<IKillReactionArtifact>(ownerState, owner, killReaction));

                if (artifact is ISkillCastReactionArtifact skillCastReaction)
                    _skillCastReactions.Add(
                        new ArtifactBinding<ISkillCastReactionArtifact>(ownerState, owner, skillCastReaction)
                    );

                if (artifact is IAttackRetargetArtifact attackRetarget)
                    _attackRetargets.Add(
                        new ArtifactBinding<IAttackRetargetArtifact>(ownerState, owner, attackRetarget)
                    );

                if (artifact is IPositionHistoryArtifact positionHistoryEffect)
                    _positionHistoryEffects.Add(
                        new ArtifactBinding<IPositionHistoryArtifact>(ownerState, owner, positionHistoryEffect)
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

    public bool CanTarget(
        BattleUnitCombatState requester,
        BattleUnitCombatState candidate,
        BattleTargetingReason reason
    )
    {
        if (!DefaultBattleTargetingPolicy.Instance.CanTarget(requester, candidate, reason))
            return false;

        for (int i = 0; i < _targetingModifiers.Count; i++)
        {
            ArtifactBinding<ITargetingModifierArtifact> binding = _targetingModifiers[i];
            if (candidate == null || binding.Owner != candidate)
                continue;
            if (!binding.Artifact.CanBeTargeted(binding.Owner, requester, reason))
                return false;
        }

        return true;
    }

    public float ModifyTargetScore(
        BattleUnitCombatState requester,
        BattleUnitCombatState candidate,
        float baseScore,
        BattleTargetingReason reason
    )
    {
        BattleTargetScore score = new BattleTargetScore
        {
            Requester = requester,
            Candidate = candidate,
            Value = baseScore,
            Reason = reason,
        };

        for (int i = 0; i < _targetingModifiers.Count; i++)
        {
            ArtifactBinding<ITargetingModifierArtifact> binding = _targetingModifiers[i];
            binding.Artifact.ModifyTargetScore(binding.Owner, ref score);
        }

        return score.Value;
    }

    public void ModifyMoveSpeed(ref BattleMoveRequest request)
    {
        for (int i = 0; i < _movementModifiers.Count; i++)
        {
            ArtifactBinding<IMovementModifierArtifact> binding = _movementModifiers[i];
            binding.Artifact.ModifyMoveSpeed(binding.Owner, ref request);
        }
    }

    public bool CanIgnoreForcedMovement(BattleRuntimeUnit target, in BattleForcedMovementRequest request)
    {
        if (target == null)
            return false;

        for (int i = 0; i < _movementModifiers.Count; i++)
        {
            ArtifactBinding<IMovementModifierArtifact> binding = _movementModifiers[i];
            if (binding.Owner != target.State)
                continue;
            if (binding.Artifact.CanIgnoreForcedMovement(binding.Owner, request))
                return true;
        }

        return false;
    }

    public void AfterDamage(in BattleDamageResult result, IBattleEffectSink effects)
    {
        for (int i = 0; i < _damageReactions.Count; i++)
        {
            ArtifactBinding<IDamageReactionArtifact> binding = _damageReactions[i];
            binding.Artifact.AfterDamage(binding.Owner, result, effects);
        }
    }

    public void AfterHeal(in BattleHealResult result, IBattleEffectSink effects)
    {
        for (int i = 0; i < _healReactions.Count; i++)
        {
            ArtifactBinding<IHealReactionArtifact> binding = _healReactions[i];
            binding.Artifact.AfterHeal(binding.Owner, result, effects);
        }
    }

    public void OnUnitKilled(in BattleKillEvent killEvent, IBattleEffectSink effects)
    {
        for (int i = 0; i < _killReactions.Count; i++)
        {
            ArtifactBinding<IKillReactionArtifact> binding = _killReactions[i];
            binding.Artifact.OnUnitKilled(binding.Owner, killEvent, effects);
        }
    }

    public void OnSkillCast(in BattleSkillCastEvent skillCastEvent, IBattleEffectSink effects)
    {
        for (int i = 0; i < _skillCastReactions.Count; i++)
        {
            ArtifactBinding<ISkillCastReactionArtifact> binding = _skillCastReactions[i];
            binding.Artifact.OnSkillCast(binding.Owner, skillCastEvent, effects);
        }
    }

    public bool TryOverrideBasicAttackTarget(
        BattleRuntimeUnit owner,
        BattleFieldSnapshot snapshot,
        out BattleRuntimeUnit target
    )
    {
        target = null;
        if (owner == null)
            return false;

        for (int i = 0; i < _attackRetargets.Count; i++)
        {
            ArtifactBinding<IAttackRetargetArtifact> binding = _attackRetargets[i];
            if (binding.OwnerView != owner)
                continue;

            if (binding.Artifact.TryOverrideBasicAttackTarget(owner, snapshot, out target))
                return true;
        }

        return false;
    }

    public void TickPositionHistoryArtifacts(
        BattlePositionHistory history,
        in BattleEffectContext context,
        IBattleEffectSink effects
    )
    {
        for (int i = 0; i < _positionHistoryEffects.Count; i++)
        {
            ArtifactBinding<IPositionHistoryArtifact> binding = _positionHistoryEffects[i];
            if (binding.OwnerView == null || binding.OwnerView.IsCombatDisabled)
                continue;

            binding.Artifact.TickWithPositionHistory(binding.OwnerView, history, context, effects);
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
