using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Training/Gladiator Reward Config")]
public sealed class GladiatorRewardConfig : ScriptableObject
{
    public float step = -0.001f;
    public float approach = 0.0005f;
    public float retreatDistance = 0.0005f;
    public float chaseTarget = 0.0005f;
    public float disengaged = -0.01f;
    public float goodRetreat = 0.001f;
    public float badRetreat = -0.02f;
    public float inRangeNoAttack = -0.01f;
    public float dangerousAttack = -0.03f;
    public float damageTaken = -0.01f;
    public float damageTakenPerPoint = -0.002f;
    public float death = -2f;
    public float damageDealt = 0.01f;
    public float attackLanded = 0.05f;
    public float kill = 10f;
    public float win = 20f;
    public float loss = -20f;
    public float timeout = -20f;
    public float boundary = -0.2f;
    public float invalidAction = -1f;
    public float actionDelta = -0.001f;
    public float turnDelta = -0.0005f;
    public float idleJitter = -0.001f;
    public float invalidSkill = -0.02f;
    public float skillActivated = 0.02f;
}
