using UnityEngine;

public readonly struct BattleUnitPose
{
    private const float Epsilon = 1e-6f;

    public readonly Vector3 Right;
    public readonly Vector3 Forward;

    public BattleUnitPose(Vector3 right, Vector3 forward)
    {
        Right = right.sqrMagnitude > Epsilon ? right.normalized : Vector3.right;
        Forward = forward.sqrMagnitude > Epsilon ? forward.normalized : Vector3.forward;
    }

    public static BattleUnitPose Default => new BattleUnitPose(Vector3.right, Vector3.forward);
}
