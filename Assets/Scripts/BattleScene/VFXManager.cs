using System;
using System.Collections.Generic;
using UnityEngine;

// 이펙트 데이터를 인스펙터에 등록하기 위한 구조체
[Serializable]
public struct VisualEffectData
{
    public string effectId;
    public GameObject prefab;
}

public class VFXManager : MonoBehaviour
{
    //싱글톤 적용
    public static VFXManager Instance { get; private set; }

    [Header("Visual Effects (VFX)")]
    [SerializeField] private List<VisualEffectData> effects;

    private Dictionary<string, GameObject> _effectDict;

    private void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 딕셔너리 세팅
        _effectDict = new Dictionary<string, GameObject>();
        if (effects != null)
        {
            foreach (var data in effects)
            {
                if (!string.IsNullOrEmpty(data.effectId) && data.prefab != null)
                    _effectDict[data.effectId] = data.prefab;
            }
        }
    }

    // 🌟 핵심: ID와 위치(선택적으로 회전값)를 받아 이펙트를 즉시 생성하는 함수
    public void PlayEffect(string effectId, Vector3 position, Quaternion rotation = default)
    {
        if (rotation == default) rotation = Quaternion.identity;

        if (_effectDict != null && _effectDict.TryGetValue(effectId, out GameObject prefab))
        {
            Instantiate(prefab, position, rotation);
        }
        else
        {
            Debug.LogWarning($"[VFXManager] '{effectId}' 이펙트가 등록되지 않았습니다!");
        }
    }
}
