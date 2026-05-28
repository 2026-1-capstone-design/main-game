using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// UI RawImage 위에 검투사 3D 프리팹을 렌더링하는 독립 프리뷰 뷰다.
// 각 셀/상세 패널이 자체 RenderTexture와 Camera를 가지므로 서로 다른 검투사를 동시에 보여줄 수 있다.
[DisallowMultipleComponent]
public sealed class GladiatorModelPreviewView : MonoBehaviour
{
    private const float PreviewSpacing = 200f;
    private static readonly SortedSet<int> s_releasedPreviewIndices = new SortedSet<int>();
    private static int s_nextPreviewIndex;

    [SerializeField]
    private RawImage targetImage;

    [SerializeField]
    private int textureSize = 256;

    [SerializeField]
    private Vector3 modelLocalEulerAngles = new Vector3(0f, 180f, 0f);

    [SerializeField]
    private Vector3 cameraOffset = new Vector3(0f, 1.2f, 4f);

    [SerializeField]
    private Vector3 lookAtOffset = new Vector3(0f, 0.15f, 0f);

    [SerializeField]
    private float cameraFieldOfView = 28f;

    [SerializeField]
    private Color backgroundColor = new Color(0f, 0f, 0f, 0f);

    [SerializeField]
    private float lightIntensity = 0.8f;

    [SerializeField]
    private Vector3 lightLocalEulerAngles = new Vector3(30f, 180f, 0f);

    [SerializeField]
    private float lightRange = 8f;

    [SerializeField]
    private Color ambientColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [SerializeField]
    private RuntimeAnimatorController previewAnimatorController;

    [SerializeField]
    private string previewAnimatorStateName = "Idle";

    private RenderTexture _renderTexture;
    private Camera _camera;
    private Light _light;
    private Transform _previewRoot;
    private GameObject _modelInstance;
    private GameObject _currentPrefab;
    private GameObject _currentLeftWeaponPrefab;
    private GameObject _currentRightWeaponPrefab;
    private int[] _currentCustomizeIndicates;
    private bool _hasAppliedCustomization;
    private int _previewIndex = -1;
    private Vector3 _previewOrigin;
    private Animator _preparedAnimator;
    private RuntimeAnimatorController _preparedAnimatorController;
    private string _preparedAnimatorStateName;

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
        if (_currentPrefab != null)
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
        ClearModel();

        if (_camera != null)
        {
            Destroy(_camera.gameObject);
            _camera = null;
        }

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }

        if (_previewRoot != null)
        {
            Destroy(_previewRoot.gameObject);
            _previewRoot = null;
        }

        ReleasePreviewIndex();
    }

    public void Show(GameObject modelPrefab)
    {
        Show(modelPrefab, null);
    }

    public void Show(
        GameObject modelPrefab,
        int[] customizeIndicates,
        GameObject leftWeaponPrefab,
        GameObject rightWeaponPrefab
    )
    {
        ShowInternal(modelPrefab, customizeIndicates, leftWeaponPrefab, rightWeaponPrefab);
    }

    public bool UsesTargetImage(RawImage image)
    {
        return image != null && targetImage == image;
    }

    public void Show(GameObject modelPrefab, int[] customizeIndicates)
    {
        ShowInternal(modelPrefab, customizeIndicates, null, null);
    }

    private void ShowInternal(
        GameObject modelPrefab,
        int[] customizeIndicates,
        GameObject leftWeaponPrefab,
        GameObject rightWeaponPrefab
    )
    {
        EnsurePreviewObjects();

        if (modelPrefab == null)
        {
            Clear();
            return;
        }

        if (_currentPrefab != modelPrefab)
        {
            ClearModel();
            _currentPrefab = modelPrefab;
            _modelInstance = Instantiate(modelPrefab, _previewRoot);
            _modelInstance.transform.localPosition = Vector3.zero;
            _modelInstance.transform.localRotation = Quaternion.Euler(modelLocalEulerAngles);
            _modelInstance.transform.localScale = Vector3.one;
            _hasAppliedCustomization = false;
            ResetPreparedAnimator();
            SetLayerRecursively(_modelInstance, gameObject.layer);
            DisableRuntimeOnlyUi(_modelInstance);
            PrepareRenderers(_modelInstance);
        }

        ApplyCustomization(customizeIndicates);
        ApplyWeaponPrefabs(leftWeaponPrefab, rightWeaponPrefab);
        PrepareAnimator(_modelInstance);
        FrameModel();
        SetVisible(true);
    }

    public void Clear()
    {
        _currentPrefab = null;
        _currentLeftWeaponPrefab = null;
        _currentRightWeaponPrefab = null;
        _currentCustomizeIndicates = null;
        _hasAppliedCustomization = false;
        ClearModel();
        SetVisible(false);
    }

    private void EnsurePreviewObjects()
    {
        if (_renderTexture == null)
        {
            int resolvedTextureSize = Mathf.Max(32, textureSize);
            _renderTexture = new RenderTexture(resolvedTextureSize, resolvedTextureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = $"{name}_PreviewTexture",
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

        GameObject cameraObject = new GameObject($"{name}_PreviewCamera");
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

        GameObject lightObject = new GameObject($"{name}_PreviewLight");
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        lightObject.transform.SetParent(cameraObject.transform, false);
        lightObject.transform.localPosition = Vector3.zero;
        lightObject.transform.localRotation = Quaternion.identity;
        _light = lightObject.AddComponent<Light>();
        _light.type = LightType.Point;
        ApplyLightingSettings();

        GameObject rootObject = new GameObject($"{name}_PreviewRoot");
        rootObject.hideFlags = HideFlags.HideAndDontSave;
        _previewRoot = rootObject.transform;
        _previewIndex = AcquirePreviewIndex();
        _previewOrigin = new Vector3(_previewIndex * PreviewSpacing, -1000f, 0f);
        _previewRoot.position = _previewOrigin;
    }

    private static int AcquirePreviewIndex()
    {
        if (s_releasedPreviewIndices.Count <= 0)
        {
            return s_nextPreviewIndex++;
        }

        int index = s_releasedPreviewIndices.Min;
        s_releasedPreviewIndices.Remove(index);
        return index;
    }

    private void ReleasePreviewIndex()
    {
        if (_previewIndex < 0)
        {
            return;
        }

        s_releasedPreviewIndices.Add(_previewIndex);
        _previewIndex = -1;
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

    private void FrameModel()
    {
        if (_modelInstance == null || _camera == null)
        {
            return;
        }

        Bounds bounds = CalculateBounds(_modelInstance);
        Vector3 center = bounds.center;
        float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
        Vector3 cameraPosition = center + cameraOffset.normalized * Mathf.Max(cameraOffset.magnitude, radius * 2.5f);
        Vector3 lookAtPosition =
            center + new Vector3(lookAtOffset.x * radius, lookAtOffset.y * radius, lookAtOffset.z * radius);

        _camera.transform.position = cameraPosition;
        _camera.transform.LookAt(lookAtPosition);
        _camera.farClipPlane = Mathf.Max(50f, radius * 8f);

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

        if (_modelInstance != null)
        {
            _modelInstance.SetActive(value);
        }
    }

    private void ClearModel()
    {
        if (_modelInstance != null)
        {
            Destroy(_modelInstance);
            _modelInstance = null;
        }

        ResetPreparedAnimator();
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

    private void PrepareAnimator(GameObject target)
    {
        Animator animator = target.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            return;
        }

        RuntimeAnimatorController controller = previewAnimatorController;
        if (controller == null && AnimationManager.Instance != null)
        {
            controller = AnimationManager.Instance.noneController;
        }

        if (controller != null && animator.runtimeAnimatorController != controller)
        {
            animator.runtimeAnimatorController = controller;
        }

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        string stateName = string.IsNullOrWhiteSpace(previewAnimatorStateName)
            ? string.Empty
            : previewAnimatorStateName.Trim();
        bool needsStateReset =
            _preparedAnimator != animator
            || _preparedAnimatorController != animator.runtimeAnimatorController
            || _preparedAnimatorStateName != stateName;

        if (needsStateReset && !string.IsNullOrEmpty(stateName) && animator.runtimeAnimatorController != null)
        {
            TryPlayAnimatorState(animator, stateName);
            animator.Update(0f);
        }

        _preparedAnimator = animator;
        _preparedAnimatorController = animator.runtimeAnimatorController;
        _preparedAnimatorStateName = stateName;
    }

    private static bool TryPlayAnimatorState(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        int shortNameHash = Animator.StringToHash(stateName);
        if (animator.HasState(0, shortNameHash))
        {
            animator.Play(shortNameHash, 0, 0f);
            return true;
        }

        int fullPathHash = Animator.StringToHash($"Base Layer.{stateName}");
        if (animator.HasState(0, fullPathHash))
        {
            animator.Play(fullPathHash, 0, 0f);
            return true;
        }

        return false;
    }

    private void ResetPreparedAnimator()
    {
        _preparedAnimator = null;
        _preparedAnimatorController = null;
        _preparedAnimatorStateName = null;
    }

    private void ApplyCustomization(int[] customizeIndicates)
    {
        if (
            _modelInstance == null
            || (_hasAppliedCustomization && AreSameCustomizeIndicates(_currentCustomizeIndicates, customizeIndicates))
        )
        {
            return;
        }

        _currentCustomizeIndicates = BuildSafeCustomizeIndicates(customizeIndicates);
        _hasAppliedCustomization = true;

        BattleRuntimeUnit runtimeUnit = _modelInstance.GetComponentInChildren<BattleRuntimeUnit>(true);
        if (runtimeUnit != null)
        {
            runtimeUnit.ApplySkinCustomization(_currentCustomizeIndicates);
        }
    }

    private void ApplyWeaponPrefabs(GameObject leftWeaponPrefab, GameObject rightWeaponPrefab)
    {
        if (
            _modelInstance == null
            || (_currentLeftWeaponPrefab == leftWeaponPrefab && _currentRightWeaponPrefab == rightWeaponPrefab)
        )
        {
            return;
        }

        _currentLeftWeaponPrefab = leftWeaponPrefab;
        _currentRightWeaponPrefab = rightWeaponPrefab;

        BattleRuntimeUnit runtimeUnit = _modelInstance.GetComponentInChildren<BattleRuntimeUnit>(true);
        if (runtimeUnit == null)
        {
            return;
        }

        runtimeUnit.ApplyWeaponPrefabs(leftWeaponPrefab, rightWeaponPrefab, true);
        PrepareRenderers(_modelInstance);
    }

    private static int[] BuildSafeCustomizeIndicates(int[] customizeIndicates)
    {
        if (customizeIndicates != null && customizeIndicates.Length > (int)SkinPart.Feet)
        {
            return (int[])customizeIndicates.Clone();
        }

        int[] fallback = new int[(int)SkinPart.TotalCount];
        fallback[(int)SkinPart.FullHead] = -1;
        fallback[(int)SkinPart.Nose] = 0;
        fallback[(int)SkinPart.Hair] = 0;
        fallback[(int)SkinPart.Face] = 0;
        fallback[(int)SkinPart.Eyes] = 0;
        fallback[(int)SkinPart.Eyebrows] = 0;
        fallback[(int)SkinPart.Ears] = 0;
        fallback[(int)SkinPart.Chest] = 0;
        fallback[(int)SkinPart.Arms] = 0;
        fallback[(int)SkinPart.Belt] = 0;
        fallback[(int)SkinPart.Legs] = 0;
        fallback[(int)SkinPart.Feet] = 0;
        return fallback;
    }

    private static void DisableRuntimeOnlyUi(GameObject target)
    {
        Canvas[] canvases = target.GetComponentsInChildren<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    private static bool AreSameCustomizeIndicates(int[] left, int[] right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
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
