public interface ITrainingActionAdapter
{
    BattleTacticalCommand Adapt(GladiatorPolicyAction action, BattleTrainingContext context);
}
