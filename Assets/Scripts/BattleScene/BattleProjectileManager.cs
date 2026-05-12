using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct CustomProjectileData
{
    public string projectileId;
    public GameObject prefab;
}

public class BattleProjectileManager : MonoBehaviour
{
    [Header("Base Resources")]
    [SerializeField]
    private Transform projectileRoot;

    [SerializeField]
    private GameObject normalArrowPrefab;

    [SerializeField]
    private GameObject normalMagicPrefab;

    [Header("Custom Projectiles")]
    [SerializeField]
    private List<CustomProjectileData> customProjectiles;

    private Dictionary<string, GameObject> _projectileDict;

    private void Awake()
    {
        _projectileDict = new Dictionary<string, GameObject>();
        if (customProjectiles != null)
        {
            foreach (var data in customProjectiles)
            {
                if (!string.IsNullOrEmpty(data.projectileId) && data.prefab != null)
                    _projectileDict[data.projectileId] = data.prefab;
            }
        }
    }

    public Transform ProjectileRoot => projectileRoot;
    public GameObject NormalArrowPrefab => normalArrowPrefab;
    public GameObject NormalMagicPrefab => normalMagicPrefab;

    public GameObject GetCustomPrefab(string id)
    {
        if (_projectileDict != null && _projectileDict.TryGetValue(id, out GameObject prefab))
            return prefab;

        Debug.LogWarning($"[BattleProjectileManager] '{id}' 이름의 투사체 프리팹이 등록되지 않았습니다!");
        return null;
    }
}
