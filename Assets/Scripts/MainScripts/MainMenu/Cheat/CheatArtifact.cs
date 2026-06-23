using UnityEngine;

public class CheatArtifact : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private InventoryManager inventoryManager;

    [Header("Cheat Settings")]
    [Tooltip("인벤토리에 강제로 추가할 장신구 데이터 (ArtifactSO)를 연결하세요.")]
    public ArtifactSO targetArtifact;

    [ContextMenu("지정 장신구 인벤토리에 강제 추가 (Cheat)")]
    public void GiveCheatArtifact()
    {
        ResolveDependencies();

        if (inventoryManager == null)
        {
            Debug.LogError("[CheatArtifact] InventoryManager가 연결되지 않았습니다.", this);
            return;
        }

        if (targetArtifact == null)
        {
            Debug.LogError("[CheatArtifact] 추가할 targetArtifact(ArtifactSO)를 지정해주세요.", this);
            return;
        }

        // 마켓에서 장신구를 구매할 때 사용하는 공식 인벤토리 추가 함수를 그대로 활용합니다.
        bool isAdded = inventoryManager.AddPurchasedArtifactFromMarketOffer(targetArtifact);

        if (isAdded)
        {
            Debug.Log($"[CheatArtifact] '{targetArtifact.name}' 장신구를 인벤토리에 성공적으로 추가했습니다!", this);
        }
        else
        {
            Debug.LogError("[CheatArtifact] 장신구를 인벤토리에 추가하는 데 실패했습니다.", this);
        }
    }

    private void ResolveDependencies()
    {
        if (inventoryManager == null)
        {
            inventoryManager =
                InventoryManager.Instance ?? FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
        }
    }
}
