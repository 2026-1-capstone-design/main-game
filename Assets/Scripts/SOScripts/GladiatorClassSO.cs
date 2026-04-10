using UnityEngine;

[CreateAssetMenu(menuName = "Prototype/Content/Gladiator Class")]
public sealed class GladiatorClassSO : ScriptableObject
{
    public Sprite icon;
    public string className = "Gladiator";

    public float baseHealth = 1000f;
    public float healthGrowthPerLevel = 100f;

    public float baseAttack = 20f;
    public float attackGrowthPerLevel = 2f;

    public float attackSpeed = 1f;
    public float moveSpeed = 3f;
    public float attackRange = 30f;
}
