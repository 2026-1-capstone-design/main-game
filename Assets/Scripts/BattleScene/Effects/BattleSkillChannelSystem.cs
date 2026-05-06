using System.Collections.Generic;

// 채널링이 중단될 때 후속 효과가 원인을 구분할 수 있게 하는 값이다.
public enum BattleInterruptReason
{
    Stunned,
    Died,
    ForcedMoved,
    NewCommand,
    BattleEnded,
}

// 여러 틱 동안 유지되는 스킬이 구현하는 채널링 생명주기 계약이다.
public interface IChanneledBattleSkill : IBattleSkill
{
    bool BlocksMovement { get; }
    bool BlocksBasicAttack { get; }
    bool BlocksDecisionChange { get; }
    float ChannelDuration { get; }

    void BeginChannel(in BattleEffectContext context, IBattleEffectSink effects);
    void TickChannel(in BattleEffectContext context, IBattleEffectSink effects);
    void CompleteChannel(in BattleEffectContext context, IBattleEffectSink effects);
    void InterruptChannel(in BattleEffectContext context, BattleInterruptReason reason, IBattleEffectSink effects);
}

// 채널링 스킬의 시작, 틱, 완료, 중단 생명주기를 관리한다.
// 개별 전투 시스템은 스킬명을 몰라도 이 시스템에 이동/공격/의사결정 차단 여부만 질의한다.
public sealed class BattleSkillChannelSystem
{
    private readonly List<ActiveChannel> _channels = new List<ActiveChannel>();
    private readonly Dictionary<BattleRuntimeUnit, int> _channelIndexByCaster =
        new Dictionary<BattleRuntimeUnit, int>();

    public void Clear()
    {
        _channels.Clear();
        _channelIndexByCaster.Clear();
    }

    public bool IsMovementBlocked(BattleRuntimeUnit unit) =>
        TryGetChannel(unit, out ActiveChannel channel) && channel.Skill.BlocksMovement;

    public bool IsBasicAttackBlocked(BattleRuntimeUnit unit) =>
        TryGetChannel(unit, out ActiveChannel channel) && channel.Skill.BlocksBasicAttack;

    public bool IsDecisionChangeBlocked(BattleRuntimeUnit unit) =>
        TryGetChannel(unit, out ActiveChannel channel) && channel.Skill.BlocksDecisionChange;

    public bool IsChanneling(BattleRuntimeUnit unit) => TryGetChannel(unit, out _);

    public void StartChannel(
        BattleRuntimeUnit caster,
        IChanneledBattleSkill skill,
        in BattleEffectContext context,
        IBattleEffectSink effects
    )
    {
        if (caster == null || skill == null || caster.IsCombatDisabled)
            return;

        InterruptChannel(caster, BattleInterruptReason.NewCommand, context, effects);
        ActiveChannel channel = new ActiveChannel(caster, context.PrimaryTarget, skill, context.BattleTime);
        _channelIndexByCaster[caster] = _channels.Count;
        _channels.Add(channel);
        skill.BeginChannel(context, effects);
    }

    public void Tick(in BattleEffectContext context, IBattleEffectSink effects)
    {
        if (_channels.Count == 0)
            return;

        for (int i = _channels.Count - 1; i >= 0; i--)
        {
            ActiveChannel channel = _channels[i];
            if (channel.Caster == null || channel.Caster.IsCombatDisabled)
            {
                InterruptAt(i, channel, BattleInterruptReason.Died, context, effects);
                continue;
            }

            if (channel.Caster.State.IsStunned)
            {
                InterruptAt(i, channel, BattleInterruptReason.Stunned, context, effects);
                continue;
            }

            channel.Elapsed += context.BattleTime - channel.LastBattleTime;
            channel.LastBattleTime = context.BattleTime;

            BattleEffectContext channelContext = new BattleEffectContext(
                channel.Caster,
                channel.PrimaryTarget,
                context.Snapshot,
                context.Units,
                context.BattleTime,
                context.BattleTick
            );
            channel.Skill.TickChannel(channelContext, effects);

            if (channel.Elapsed < channel.Skill.ChannelDuration)
            {
                _channels[i] = channel;
                continue;
            }

            RemoveAt(i, channel);
            channel.Skill.CompleteChannel(channelContext, effects);
        }
    }

    public void InterruptChannel(
        BattleRuntimeUnit caster,
        BattleInterruptReason reason,
        in BattleEffectContext context,
        IBattleEffectSink effects
    )
    {
        if (caster == null)
            return;

        if (!_channelIndexByCaster.TryGetValue(caster, out int index))
            return;

        InterruptAt(index, _channels[index], reason, context, effects);
    }

    private void InterruptAt(
        int index,
        ActiveChannel channel,
        BattleInterruptReason reason,
        in BattleEffectContext context,
        IBattleEffectSink effects
    )
    {
        RemoveAt(index, channel);
        BattleEffectContext channelContext = new BattleEffectContext(
            channel.Caster,
            channel.PrimaryTarget,
            context.Snapshot,
            context.Units,
            context.BattleTime,
            context.BattleTick
        );
        channel.Skill.InterruptChannel(channelContext, reason, effects);
    }

    private bool TryGetChannel(BattleRuntimeUnit unit, out ActiveChannel channel)
    {
        if (unit != null && _channelIndexByCaster.TryGetValue(unit, out int index) && index < _channels.Count)
        {
            channel = _channels[index];
            if (channel.Caster == unit)
                return true;
        }

        channel = default;
        return false;
    }

    private void RemoveAt(int index, ActiveChannel channel)
    {
        if (channel.Caster != null)
            _channelIndexByCaster.Remove(channel.Caster);

        int lastIndex = _channels.Count - 1;
        if (index != lastIndex)
        {
            ActiveChannel moved = _channels[lastIndex];
            _channels[index] = moved;
            if (moved.Caster != null)
                _channelIndexByCaster[moved.Caster] = index;
        }

        _channels.RemoveAt(lastIndex);
    }

    private struct ActiveChannel
    {
        public BattleRuntimeUnit Caster { get; }
        public BattleRuntimeUnit PrimaryTarget { get; }
        public IChanneledBattleSkill Skill { get; }
        public float Elapsed;
        public float LastBattleTime;

        public ActiveChannel(
            BattleRuntimeUnit caster,
            BattleRuntimeUnit primaryTarget,
            IChanneledBattleSkill skill,
            float battleTime
        )
        {
            Caster = caster;
            PrimaryTarget = primaryTarget;
            Skill = skill;
            Elapsed = 0f;
            LastBattleTime = battleTime;
        }
    }
}
