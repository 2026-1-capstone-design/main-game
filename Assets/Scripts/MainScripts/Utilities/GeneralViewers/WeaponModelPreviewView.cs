using UnityEngine;
using UnityEngine.UI;

// UI RawImage 위에 무기 3D 프리팹만 렌더링하는 전용 프리뷰 뷰다.
// 검투사 모델 프레이밍과 무기 단독 전시 프레이밍이 다르기 때문에 GladiatorModelPreviewView와 분리한다.
[DisallowMultipleComponent]
public sealed class WeaponModelPreviewView : MonoBehaviour
{
    private const float PreviewSpacing = 1000f;
    private static int s_nextPreviewIndex;

    [SerializeField]
    private RawImage targetImage;

    [SerializeField]
    private int textureSize = 256;

    [SerializeField]
    private Vector3 weaponLocalEulerAngles = new Vector3(0f, 0f, -35f);

    [SerializeField]
    private Vector3 leftWeaponLocalPosition = new Vector3(-0.12f, 0f, 0f);

    [SerializeField]
    private Vector3 rightWeaponLocalPosition = new Vector3(0.12f, 0f, 0f);

    [SerializeField]
    private Vector3 weaponLocalScale = Vector3.one;

    [SerializeField]
    private Vector3 cameraOffset = new Vector3(0f, 0.1f, 1.8f);

    [SerializeField]
    private Vector3 lookAtOffset = Vector3.zero;

    [SerializeField]
    private float cameraFieldOfView = 22f;

    [SerializeField]
    private Color backgroundColor = new Color(0f, 0f, 0f, 0f);

    [SerializeField]
    private float lightIntensity = 0.9f;

    [SerializeField]
    private Vector3 lightLocalEulerAngles = new Vector3(30f, 180f, 0f);

    [SerializeField]
    private float lightRange = 6f;

    private RenderTexture _renderTexture;
    private Camera _camera;
    private Light _light;
    private Transform _previewRoot;
    private GameObject _weaponRoot;
    private GameObject _currentLeftWeaponPrefab;
    private GameObject _currentRightWeaponPrefab;
    private Vector3 _previewOrigin;

    private void Awake()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<RawImage>();
        }

        EnsurePreviewObjects();
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (_weaponRoot != null)
        {
            SetVisible(true);
        }
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    private void OnDestroy()
    {
        ClearWeaponRoot();

        if (_camera != null)
        {
            Destroy(_camera.gameObject);
        }

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
        }
    }

    public void Show(GameObject leftWeaponPrefab, GameObject rightWeaponPrefab)
    {
        EnsurePreviewObjects();

        if (leftWeaponPrefab == null && rightWeaponPrefab == null)
        {
            Clear();
            return;
        }

        if (_currentLeftWeaponPrefab != leftWeaponPrefab || _currentRightWeaponPrefab != rightWeaponPrefab)
        {
            ClearWeaponRoot();
            _currentLeftWeaponPrefab = leftWeaponPrefab;
            _currentRightWeaponPrefab = rightWeaponPrefab;
            _weaponRoot = new GameObject("WeaponPreviewRoot");
            _weaponRoot.transform.SetParent(_previewRoot, false);
            _weaponRoot.transform.localPosition = Vector3.zero;
            _weaponRoot.transform.localRotation = Quaternion.Euler(weaponLocalEulerAngles);
            _weaponRoot.transform.localScale = weaponLocalScale;

            InstantiateWeapon(leftWeaponPrefab, leftWeaponLocalPosition);
            InstantiateWeapon(rightWeaponPrefab, rightWeaponLocalPosition);
            SetLayerRecursively(_weaponRoot, gameObject.layer);
            PrepareRenderers(_weaponRoot);
        }

        FrameWeapon();
        SetVisible(true);
    }

    public void Clear()
    {
        _currentLeftWeaponPrefab = null;
        _currentRightWeaponPrefab = null;
        ClearWeaponRoot();
        SetVisible(false);
    }

    public bool UsesTargetImage(RawImage image)
    {
        return image != null && targetImage == image;
    }

    private void EnsurePreviewObjects()
    {
        if (_renderTexture == null)
        {
            int resolvedTextureSize = Mathf.Max(32, textureSize);
            _renderTexture = new RenderTexture(resolvedTextureSize, resolvedTextureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = $"{name}_WeaponPreviewTexture",
            };
            _renderTexture.Create();
        }

        if (targetImage != null)
        {
            targetImage.texture = _renderTexture;
        }

        if (_camera != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject($"{name}_WeaponPreviewCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        _camera = cameraObject.AddComponent<Camera>();
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = backgroundColor;
        _camera.orthographic = false;
        _camera.fieldOfView = cameraFieldOfView;
        _camera.cullingMask = 1 << gameObject.layer;
        _camera.nearClipPlane = 0.01f;
        _camera.farClipPlane = 100f;
        _camera.targetTexture = _renderTexture;
        _camera.enabled = false;

        GameObject lightObject = new GameObject($"{name}_WeaponPreviewLight");
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        lightObject.transform.SetParent(cameraObject.transform, false);
        lightObject.transform.localPosition = Vector3.zero;
        _light = lightObject.AddComponent<Light>();
        _light.type = LightType.Point;
        ApplyLightingSettings();

        GameObject rootObject = new GameObject($"{name}_WeaponPreviewRoot");
        rootObject.hideFlags = HideFlags.HideAndDontSave;
        _previewRoot = rootObject.transform;
        _previewOrigin = new Vector3(s_nextPreviewIndex++ * PreviewSpacing, -12000f, 0f);
        _previewRoot.position = _previewOrigin;
    }

    private void OnValidate()
    {
        if (_camera != null)
        {
            _camera.backgroundColor = backgroundColor;
            _camera.fieldOfView = cameraFieldOfView;
        }

        ApplyLightingSettings();
    }

    private void InstantiateWeapon(GameObject weaponPrefab, Vector3 localPosition)
    {
        if (weaponPrefab == null || _weaponRoot == null)
        {
            return;
        }

        GameObject weaponInstance = Instantiate(weaponPrefab, _weaponRoot.transform);
        weaponInstance.transform.localPosition = localPosition;
    }

    private void FrameWeapon()
    {
        if (_weaponRoot == null || _camera == null)
        {
            return;
        }

        Bounds bounds = CalculateBounds(_weaponRoot);
        Vector3 center = bounds.center;
        float radius = Mathf.Max(bounds.extents.magnitude, 0.15f);
        Vector3 cameraPosition = center + cameraOffset.normalized * Mathf.Max(cameraOffset.magnitude, radius * 1.8f);
        Vector3 lookAtPosition =
            center + new Vector3(lookAtOffset.x * radius, lookAtOffset.y * radius, lookAtOffset.z * radius);

        _camera.transform.position = cameraPosition;
        _camera.transform.LookAt(lookAtPosition);
        _camera.farClipPlane = Mathf.Max(20f, radius * 8f);

        if (_light != null)
        {
            _light.range = Mathf.Max(lightRange, radius * 6f);
        }
    }

    private void ApplyLightingSettings()
    {
        if (_light == null)
        {
            return;
        }

        _light.intensity = Mathf.Max(0f, lightIntensity);
        _light.color = Color.white;
        _light.transform.localRotation = Quaternion.Euler(lightLocalEulerAngles);
        _light.range = Mathf.Max(0.1f, lightRange);
        _light.cullingMask = 1 << gameObject.layer;
        _light.renderMode = LightRenderMode.ForcePixel;
    }

    private void SetVisible(bool value)
    {
        if (targetImage != null)
        {
            targetImage.enabled = value;
        }

        if (_camera != null)
        {
            _camera.enabled = value;
        }

        if (_light != null)
        {
            _light.enabled = value;
        }

        if (_weaponRoot != null)
        {
            _weaponRoot.SetActive(value);
        }
    }

    private void ClearWeaponRoot()
    {
        if (_weaponRoot != null)
        {
            Destroy(_weaponRoot);
            _weaponRoot = null;
        }
    }

    private static Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return new Bounds(target.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void PrepareRenderers(GameObject target)
    {
        SkinnedMeshRenderer[] skinnedMeshRenderers = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
        {
            skinnedMeshRenderer.updateWhenOffscreen = true;
        }
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }

        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
        {
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }
    }
}
