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
    [SerializeField]
    private List<VisualEffectData> effects;

    private Dictionary<string, GameObject> _effectDict;

    private void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

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

    // 단발성/고정 위치 이펙트 생성 (생성된 VFX를 반환)
    public GameObject PlayEffect(string effectId, Vector3 position, Quaternion rotation = default)
    {
        if (_effectDict != null && _effectDict.TryGetValue(effectId, out GameObject prefab))
        {
            if (rotation.Equals(default(Quaternion)))
            {
                rotation = prefab.transform.rotation;
            }

            return Instantiate(prefab, position, rotation);
        }
        else
        {
            Debug.LogWarning($"[VFXManager] '{effectId}' 이펙트가 등록되지 않았습니다!");
            return null;
        }
    }

    // 유닛을 따라다니는 유지형 이펙트 (부모 Transform 지정)
    public GameObject PlayEffect(string effectId, Transform parent, Vector3 localPosition = default)
    {
        if (_effectDict != null && _effectDict.TryGetValue(effectId, out GameObject prefab))
        {
            // 부모(유닛)의 하위 객체로 생성하여 유닛이 이동할 때 이펙트도 함께 이동하도록 설정
            GameObject effectInstance = Instantiate(prefab, parent);
            effectInstance.transform.localPosition = localPosition + Vector3.up;
            return effectInstance;
        }

        Debug.LogWarning($"[VFXManager] '{effectId}' 부착형 이펙트가 등록되지 않았습니다!");
        return null;
    }

    // 유지되던 이펙트를 강제로 종료/삭제
    public void StopEffect(GameObject effectInstance)
    {
        if (effectInstance != null)
        {
            Destroy(effectInstance);
        }
    }
}
