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

    // 🌟 수정됨: 단발성/고정 위치 이펙트 생성 (생성된 GameObject를 반환하도록 변경)
    public GameObject PlayEffect(string effectId, Vector3 position, Quaternion rotation = default)
    {
        if (rotation == default)
            rotation = Quaternion.identity;

        if (_effectDict != null && _effectDict.TryGetValue(effectId, out GameObject prefab))
        {
            return Instantiate(prefab, position, rotation);
        }
        else
        {
            Debug.LogWarning($"[VFXManager] '{effectId}' 이펙트가 등록되지 않았습니다!");
            return null;
        }
    }

    // 🌟 신규 추가: 유닛을 따라다니는 유지형 이펙트 (부모 Transform 지정)
    public GameObject PlayEffect(string effectId, Transform parent, Vector3 localPosition = default)
    {
        if (_effectDict != null && _effectDict.TryGetValue(effectId, out GameObject prefab))
        {
            // 부모(유닛)의 하위 객체로 생성하여 유닛이 이동할 때 이펙트도 함께 이동하도록 설정
            GameObject effectInstance = Instantiate(prefab, parent);
            effectInstance.transform.localPosition = localPosition;
            return effectInstance;
        }

        Debug.LogWarning($"[VFXManager] '{effectId}' 부착형 이펙트가 등록되지 않았습니다!");
        return null;
    }

    // 🌟 신규 추가: 유지되던 이펙트를 강제로 종료/삭제
    public void StopEffect(GameObject effectInstance)
    {
        if (effectInstance != null)
        {
            // 당장은 즉시 파괴(Destroy)를 사용하지만,
            // 나중에 파티클이 서서히 사라지게 하려면 여기에 ParticleSystem.Stop() 로직을 넣으시면 됩니다!
            Destroy(effectInstance);
        }
    }
}
