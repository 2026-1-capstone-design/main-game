using UnityEngine;

public abstract class SingletonBehaviour<T> : MonoBehaviour
    where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    protected bool IsPrimaryInstance => ReferenceEquals(Instance, this);

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                $"[{typeof(T).Name}] Duplicate instance detected on '{gameObject.name}'. Destroying duplicate root.",
                this
            );
            Destroy(gameObject);
            return;
        }

        Instance = this as T;
    }

    protected virtual void OnDestroy()
    {
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }
}
