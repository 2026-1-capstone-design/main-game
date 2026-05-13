using UnityEngine;

public class BattleProjectileVisual : MonoBehaviour
{
    public void SyncPosition(Vector3 newPosition)
    {
        Vector3 direction = newPosition - transform.position;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);

        transform.position = newPosition;
    }
}
