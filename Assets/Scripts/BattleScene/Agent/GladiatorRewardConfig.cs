using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Training/Gladiator Reward Config")]
public sealed class GladiatorRewardConfig : ScriptableObject
{
    public float damageTaken = -0.1f;
    public float death = -5f;
    public float attackLanded = 10f;
    public float kill = 100f;
    public float boundary = -1f;
    public float distanceShapingScale = 0.005f;
    public float invalidAction = -10f;
}
