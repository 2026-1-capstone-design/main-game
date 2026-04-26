using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Training/Gladiator Reward Config")]
public sealed class GladiatorRewardConfig : ScriptableObject
{
    public float step = -0.01f;
    public float approach = 0.01f;
    public float inRangeNoAttack = -0.02f;
    public float damageTaken = -0.1f;
    public float death = -5f;
    public float attackLanded = 10f;
    public float kill = 100f;
    public float win = 50f;
    public float loss = -50f;
    public float timeout = -25f;
    public float boundary = -1f;
    public float invalidAction = -10f;
}
