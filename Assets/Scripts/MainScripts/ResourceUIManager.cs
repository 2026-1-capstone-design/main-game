using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ResourceUIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private string goldPrefix = "Gold : ";

    private ResourceManager _resourceManager;

    public void Initialize(ResourceManager resourceManager)
    {
        if (_resourceManager != null)
        {
            _resourceManager.GoldChanged -= OnGoldChanged;
        }

        _resourceManager = resourceManager;

        if (_resourceManager == null)
        {
            Debug.LogError("[ResourceUIManager] resourceManager is null.", this);
            return;
        }

        _resourceManager.GoldChanged += OnGoldChanged;
        SyncNow();
    }

    public void RefreshNow()
    {
        SyncNow();
    }

    private void OnDestroy()
    {
        if (_resourceManager != null)
        {
            _resourceManager.GoldChanged -= OnGoldChanged;
        }
    }

    private void SyncNow()
    {
        if (_resourceManager == null)
        {
            return;
        }

        OnGoldChanged(_resourceManager.CurrentGold);
    }

    // 이벤트를 여기서 받음
    private void OnGoldChanged(int currentGold)
    {
        if (goldText == null)
        {
            Debug.LogWarning("[ResourceUIManager] goldText is null.", this);
            return;
        }

        goldText.text = goldPrefix + currentGold;
        Debug.Log($"[ResourceUIManager] Gold text refreshed. CurrentGold={currentGold}", this);
    }
}
