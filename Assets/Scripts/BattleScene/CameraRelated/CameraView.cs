using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

[DisallowMultipleComponent]
public sealed class CameraView : MonoBehaviour
{
    [Header("References")]
    public Transform centerTarget;   // 경기장 중심
    public Transform startPoint;     // 시작점
    public Camera targetCamera;

    [Header("Orbit")]
    public float orbitRotationSpeed = 60f;

    [Header("Look")]
    public float lookRotationSpeed = 60f;
    public float lookUpLimit = 35f;
    public float lookDownLimit = 25f;
    public float lookLeftLimit = 50f;
    public float lookRightLimit = 50f;

    [Header("Zoom (FOV Only)")]
    [Tooltip("We keep the camera on the same spectator ring and zoom by FOV only for consistency.")]
    public float defaultFov = 60f;
    public float minFov = 20f;
    public float maxFov = 80f;
    public float zoomSpeed = 60f;

    [Header("Look Compensation By Zoom")]
    [Range(0f, 1f)]
    [Tooltip("0 = no compensation, 1 = full compensation based on current FOV ratio.")]
    public float zoomLookCompensationStrength = 1f;

    private float _orbitAngle;
    private float _orbitRadius;
    private float _heightOffset;

    private float _lookYawOffset;
    private float _lookPitchOffset;

    private float _currentFov;
    private bool _isInitialized;
    private GameObject _cachedSelectedGameObject;
    private TMP_InputField _cachedInputField;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null)
        {
            targetCamera = GetComponentInChildren<Camera>();
        }
    }

    private void Start()
    {
        InitializeCameraState();
    }

    private void OnValidate()
    {
        if (maxFov < minFov)
        {
            maxFov = minFov;
        }

        defaultFov = Mathf.Clamp(defaultFov, minFov, maxFov);
        lookUpLimit = Mathf.Max(0f, lookUpLimit);
        lookDownLimit = Mathf.Max(0f, lookDownLimit);
        lookLeftLimit = Mathf.Max(0f, lookLeftLimit);
        lookRightLimit = Mathf.Max(0f, lookRightLimit);
        orbitRotationSpeed = Mathf.Max(0f, orbitRotationSpeed);
        lookRotationSpeed = Mathf.Max(0f, lookRotationSpeed);
        zoomSpeed = Mathf.Max(0f, zoomSpeed);
    }

    private void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        // 텍스트 입력창이 포커스된 동안 카메라 입력을 완전히 잠근다.
        bool isTextInputFocused = IsTextInputFocused();

        float orbitInput = 0f;
        float lookYawInput = 0f;
        float lookPitchInput = 0f;
        float zoomKeyInput = 0f;
        float scrollInput = 0f;

        // 입력이 잠겨 있어도 카메라 위치와 회전 갱신은 계속 유지한다.
        if (!isTextInputFocused && Keyboard.current != null)
        {
            if (Keyboard.current.qKey.isPressed)
                orbitInput += 1f;
            if (Keyboard.current.eKey.isPressed)
                orbitInput -= 1f;

            if (Keyboard.current.aKey.isPressed)
                lookYawInput -= 1f;
            if (Keyboard.current.dKey.isPressed)
                lookYawInput += 1f;

            if (Keyboard.current.wKey.isPressed)
                lookPitchInput += 1f;
            if (Keyboard.current.sKey.isPressed)
                lookPitchInput -= 1f;

            if (Keyboard.current.rKey.isPressed)
                zoomKeyInput += 1f;
            if (Keyboard.current.fKey.isPressed)
                zoomKeyInput -= 1f;
        }

        if (!isTextInputFocused && Mouse.current != null)
        {
            scrollInput = Mouse.current.scroll.ReadValue().y * 0.01f;
        }

        if (!isTextInputFocused)
        {
            UpdateOrbit(orbitInput);
            UpdateZoom(zoomKeyInput, scrollInput);
            UpdateLookOffset(lookYawInput, lookPitchInput);
        }

        ApplyCameraTransform();
    }

    private bool IsTextInputFocused()
    {
        // 현재 선택된 UI가 TMP 입력 필드면 입력 중으로 판단한다.
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
        {
            _cachedSelectedGameObject = null;
            _cachedInputField = null;
            return false;
        }

        GameObject selectedGameObject = EventSystem.current.currentSelectedGameObject;
        if (selectedGameObject != _cachedSelectedGameObject)
        {
            _cachedSelectedGameObject = selectedGameObject;
            _cachedInputField = selectedGameObject.GetComponentInParent<TMP_InputField>();
        }

        if (_cachedInputField == null)
        {
            return false;
        }

        // 실제 포커스가 살아있는 상태에서만 카메라를 잠근다.
        return _cachedInputField.isFocused;
    }

    private void InitializeCameraState()
    {
        if (centerTarget == null)
        {
            Debug.LogError("[CameraView] centerTarget is not assigned.", this);
            enabled = false;
            return;
        }

        if (startPoint == null)
        {
            Debug.LogError("[CameraView] startPoint is not assigned.", this);
            enabled = false;
            return;
        }

        if (targetCamera == null)
        {
            Debug.LogError("[CameraView] No Camera component found.", this);
            enabled = false;
            return;
        }

        Vector3 centerPosition = centerTarget.position;
        Vector3 startPosition = startPoint.position;

        transform.position = startPosition;

        Vector3 offset = startPosition - centerPosition;
        Vector3 flatOffset = Vector3.ProjectOnPlane(offset, Vector3.up);

        _orbitRadius = flatOffset.magnitude;
        _heightOffset = offset.y;

        if (_orbitRadius <= 0.001f)
        {
            Debug.LogWarning("[CameraView] startPoint is vertically aligned with centerTarget. Orbit radius was too small, so a fallback radius was applied.", this);
            _orbitRadius = 0.01f;
            flatOffset = Vector3.forward * _orbitRadius;
        }

        _orbitAngle = Mathf.Atan2(flatOffset.x, flatOffset.z) * Mathf.Rad2Deg;
        _lookYawOffset = 0f;
        _lookPitchOffset = 0f;
        _currentFov = defaultFov;

        targetCamera.fieldOfView = _currentFov;
        _isInitialized = true;

        ApplyCameraTransform();
    }

    private void UpdateOrbit(float orbitInput)
    {
        if (Mathf.Abs(orbitInput) <= 0.001f)
        {
            return;
        }

        _orbitAngle += orbitInput * orbitRotationSpeed * Time.deltaTime;
    }

    private void UpdateZoom(float zoomKeyInput, float scrollInput)
    {
        if (Mathf.Abs(zoomKeyInput) > 0.001f)
        {
            _currentFov -= zoomKeyInput * zoomSpeed * Time.deltaTime;
        }

        if (Mathf.Abs(scrollInput) > 0.001f)
        {
            _currentFov -= scrollInput * zoomSpeed;
        }

        _currentFov = Mathf.Clamp(_currentFov, minFov, maxFov);
        targetCamera.fieldOfView = _currentFov;
    }

    private void UpdateLookOffset(float lookYawInput, float lookPitchInput)
    {
        float compensatedLookSpeed = GetCompensatedLookSpeed();

        if (Mathf.Abs(lookYawInput) > 0.001f)
        {
            _lookYawOffset += lookYawInput * compensatedLookSpeed * Time.deltaTime;
        }

        if (Mathf.Abs(lookPitchInput) > 0.001f)
        {
            _lookPitchOffset += lookPitchInput * compensatedLookSpeed * Time.deltaTime;
        }

        _lookYawOffset = Mathf.Clamp(_lookYawOffset, -lookLeftLimit, lookRightLimit);
        _lookPitchOffset = Mathf.Clamp(_lookPitchOffset, -lookDownLimit, lookUpLimit);
    }

    private float GetCompensatedLookSpeed()
    {
        float safeDefaultFov = Mathf.Max(0.01f, defaultFov);
        float fovRatio = Mathf.Clamp(_currentFov / safeDefaultFov, 0.01f, 10f);
        float compensationScale = Mathf.Lerp(1f, fovRatio, zoomLookCompensationStrength);
        return lookRotationSpeed * compensationScale;
    }

    private void ApplyCameraTransform()
    {
        Vector3 centerPosition = centerTarget.position;

        float orbitRadians = _orbitAngle * Mathf.Deg2Rad;
        Vector3 flatOffset = new Vector3(
            Mathf.Sin(orbitRadians) * _orbitRadius,
            0f,
            Mathf.Cos(orbitRadians) * _orbitRadius
        );

        Vector3 cameraPosition = centerPosition + flatOffset + Vector3.up * _heightOffset;
        transform.position = cameraPosition;

        Vector3 baseForward = centerPosition - cameraPosition;
        if (baseForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion baseRotation = Quaternion.LookRotation(baseForward.normalized, Vector3.up);
        Quaternion lookOffsetRotation = Quaternion.Euler(-_lookPitchOffset, _lookYawOffset, 0f);

        transform.rotation = baseRotation * lookOffsetRotation;
    }
}
