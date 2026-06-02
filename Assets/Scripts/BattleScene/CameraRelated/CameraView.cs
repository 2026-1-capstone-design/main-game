// 경기장 중심 orbit 카메라를 갱신한다.
// 수동 입력과 자동 카메라 요청을 같은 상태값으로 처리한다.
// 자동 포커싱 목표는 기존 orbit/look/FOV 제약 안으로 투영한다.
// 외부 시스템은 CameraViewState만 캡처, 보간, 복원한다.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public struct CameraViewState
{
    public float OrbitAngle;
    public float LookYawOffset;
    public float LookPitchOffset;
    public float FieldOfView;

    public CameraViewState(float orbitAngle, float lookYawOffset, float lookPitchOffset, float fieldOfView)
    {
        OrbitAngle = orbitAngle;
        LookYawOffset = lookYawOffset;
        LookPitchOffset = lookPitchOffset;
        FieldOfView = fieldOfView;
    }
}

[DisallowMultipleComponent]
public sealed class CameraView : MonoBehaviour
{
    [Header("References")]
    public Transform centerTarget; // 경기장 중심
    public Transform startPoint; // 시작점
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
    public float minFov = 5f;
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

    public bool IsInitialized => _isInitialized;
    public bool WasManualCameraInputThisFrame { get; private set; }

    public event Action OnManualCameraInput;

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
        WasManualCameraInputThisFrame = false;

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

        WasManualCameraInputThisFrame =
            !isTextInputFocused
            && (
                Mathf.Abs(orbitInput) > 0.001f
                || Mathf.Abs(lookYawInput) > 0.001f
                || Mathf.Abs(lookPitchInput) > 0.001f
                || Mathf.Abs(zoomKeyInput) > 0.001f
                || Mathf.Abs(scrollInput) > 0.001f
            );

        if (WasManualCameraInputThisFrame)
        {
            OnManualCameraInput?.Invoke();
        }

        if (!isTextInputFocused)
        {
            UpdateOrbit(orbitInput);
            UpdateZoom(zoomKeyInput, scrollInput);
            UpdateLookOffset(lookYawInput, lookPitchInput);
        }

        ApplyCameraTransform();
    }

    public CameraViewState CaptureState()
    {
        return new CameraViewState(_orbitAngle, _lookYawOffset, _lookPitchOffset, _currentFov);
    }

    public void ApplyStateImmediate(CameraViewState state)
    {
        _orbitAngle = state.OrbitAngle;
        _lookYawOffset = Mathf.Clamp(state.LookYawOffset, -lookLeftLimit, lookRightLimit);
        _lookPitchOffset = Mathf.Clamp(state.LookPitchOffset, -lookDownLimit, lookUpLimit);
        _currentFov = Mathf.Clamp(state.FieldOfView, minFov, maxFov);

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = _currentFov;
        }

        if (_isInitialized)
        {
            ApplyCameraTransform();
        }
    }

    public void ApplyStateInterpolated(CameraViewState from, CameraViewState to, float t)
    {
        float clampedT = Mathf.Clamp01(t);
        CameraViewState state = new CameraViewState(
            Mathf.LerpAngle(from.OrbitAngle, to.OrbitAngle, clampedT),
            Mathf.Lerp(from.LookYawOffset, to.LookYawOffset, clampedT),
            Mathf.Lerp(from.LookPitchOffset, to.LookPitchOffset, clampedT),
            Mathf.Lerp(from.FieldOfView, to.FieldOfView, clampedT)
        );
        ApplyStateImmediate(state);
    }

    public Vector3 EvaluateCameraPosition(CameraViewState state)
    {
        return ComputeCameraPosition(state.OrbitAngle);
    }

    // 유닛 기준 선호 원의 한 지점을 현재 경기장 중심 orbit 제약 안으로 투영한다.
    // preferredDistance는 100을 기본 거리로 보고, 그보다 작으면 FOV 줌인을 적용한다.
    public bool TryBuildFocusStateForTargetCircleAngle(
        Vector3 targetPosition,
        float targetCircleAngleDegrees,
        float preferredDistance,
        float preferredElevationDegrees,
        out CameraViewState state
    )
    {
        state = default;

        if (!_isInitialized)
        {
            return false;
        }

        float safeDistance = Mathf.Clamp(preferredDistance, 0.01f, 100f);
        float elevationRadians = Mathf.Clamp(preferredElevationDegrees, -89f, 89f) * Mathf.Deg2Rad;
        float horizontalDistance = Mathf.Cos(elevationRadians) * safeDistance;
        float heightOffset = Mathf.Sin(elevationRadians) * safeDistance;
        float circleRadians = targetCircleAngleDegrees * Mathf.Deg2Rad;
        Vector3 horizontalDirection = new Vector3(Mathf.Sin(circleRadians), 0f, Mathf.Cos(circleRadians));
        Vector3 preferredPosition = targetPosition + horizontalDirection * horizontalDistance + Vector3.up * heightOffset;

        if (!TryBuildFocusStateForPreferredPosition(targetPosition, preferredPosition, out state))
        {
            return false;
        }

        state.FieldOfView = BuildAutomaticFocusFov(safeDistance);
        return true;
    }

    public bool TryBuildFocusStateForPreferredPosition(
        Vector3 targetPosition,
        Vector3 preferredWorldPosition,
        out CameraViewState state
    )
    {
        state = default;

        if (!_isInitialized || centerTarget == null)
        {
            return false;
        }

        Vector3 centerPosition = centerTarget.position;
        Vector3 flatOffset = Vector3.ProjectOnPlane(preferredWorldPosition - centerPosition, Vector3.up);
        if (flatOffset.sqrMagnitude <= 0.0001f)
        {
            flatOffset = Vector3.ProjectOnPlane(transform.position - centerPosition, Vector3.up);
        }

        if (flatOffset.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        float orbitAngle = Mathf.Atan2(flatOffset.x, flatOffset.z) * Mathf.Rad2Deg;
        return TryBuildFocusStateAtOrbitAngle(targetPosition, orbitAngle, out state);
    }

    public bool TryBuildFocusStateAtOrbitAngle(
        Vector3 targetPosition,
        float orbitAngle,
        out CameraViewState state
    )
    {
        state = default;

        if (!_isInitialized || centerTarget == null)
        {
            return false;
        }

        Vector3 cameraPosition = ComputeCameraPosition(orbitAngle);
        Vector3 baseForward = centerTarget.position - cameraPosition;
        Vector3 desiredForward = targetPosition - cameraPosition;

        if (baseForward.sqrMagnitude <= 0.0001f || desiredForward.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        if (!TryComputeNoRollLookOffsets(baseForward, desiredForward, out float lookYawOffset, out float lookPitchOffset))
        {
            return false;
        }

        if (lookYawOffset < -lookLeftLimit || lookYawOffset > lookRightLimit)
        {
            return false;
        }

        if (lookPitchOffset < -lookDownLimit || lookPitchOffset > lookUpLimit)
        {
            return false;
        }

        state = new CameraViewState(orbitAngle, lookYawOffset, lookPitchOffset, _currentFov);
        return true;
    }

    // 자동 포커싱 거리 100을 defaultFov로 보고, 더 가까운 선호 거리는 FOV 줌인으로 처리한다.
    private float BuildAutomaticFocusFov(float preferredDistance)
    {
        float clampedDistance = Mathf.Clamp(preferredDistance, 0.01f, 100f);
        float distanceRatio = clampedDistance / 100f;
        return Mathf.Clamp(defaultFov * distanceRatio, minFov, maxFov);
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
            Debug.LogWarning(
                "[CameraView] startPoint is vertically aligned with centerTarget. Orbit radius was too small, so a fallback radius was applied.",
                this
            );
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
        if (centerTarget == null)
        {
            return;
        }

        Vector3 cameraPosition = ComputeCameraPosition(_orbitAngle);
        transform.position = cameraPosition;
        transform.rotation = BuildNoRollCameraRotation(cameraPosition, _lookYawOffset, _lookPitchOffset);
    }

    // yaw는 월드 Y축, pitch는 yaw 적용 후의 수평 right axis 기준으로 적용한다.
    private Quaternion BuildNoRollCameraRotation(Vector3 cameraPosition, float lookYawOffset, float lookPitchOffset)
    {
        Vector3 baseForward = centerTarget.position - cameraPosition;
        if (baseForward.sqrMagnitude <= 0.0001f)
        {
            return transform.rotation;
        }

        Vector3 yawedForward = Quaternion.AngleAxis(lookYawOffset, Vector3.up) * baseForward.normalized;
        Vector3 rightAxis = Vector3.Cross(Vector3.up, yawedForward);

        if (rightAxis.sqrMagnitude <= 0.0001f)
        {
            rightAxis = transform.right;
        }

        rightAxis.Normalize();

        Vector3 finalForward = Quaternion.AngleAxis(-lookPitchOffset, rightAxis) * yawedForward;
        if (finalForward.sqrMagnitude <= 0.0001f)
        {
            return transform.rotation;
        }

        return Quaternion.LookRotation(finalForward.normalized, Vector3.up);
    }

    // 자동 포커싱 후보 계산도 no-roll 회전 모델과 같은 yaw/pitch 기준을 사용한다.
    private static bool TryComputeNoRollLookOffsets(
        Vector3 baseForward,
        Vector3 desiredForward,
        out float lookYawOffset,
        out float lookPitchOffset
    )
    {
        lookYawOffset = 0f;
        lookPitchOffset = 0f;

        Vector3 baseFlat = Vector3.ProjectOnPlane(baseForward, Vector3.up);
        Vector3 desiredFlat = Vector3.ProjectOnPlane(desiredForward, Vector3.up);

        if (baseFlat.sqrMagnitude <= 0.0001f || desiredFlat.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        lookYawOffset = Vector3.SignedAngle(baseFlat.normalized, desiredFlat.normalized, Vector3.up);

        Vector3 yawedForward = Quaternion.AngleAxis(lookYawOffset, Vector3.up) * baseForward.normalized;
        Vector3 rightAxis = Vector3.Cross(Vector3.up, yawedForward);

        if (rightAxis.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        rightAxis.Normalize();

        float pitchAngle = Vector3.SignedAngle(yawedForward, desiredForward.normalized, rightAxis);
        lookPitchOffset = -pitchAngle;

        return true;
    }

    private Vector3 ComputeCameraPosition(float orbitAngle)
    {
        Vector3 centerPosition = centerTarget != null ? centerTarget.position : Vector3.zero;
        float orbitRadians = orbitAngle * Mathf.Deg2Rad;
        Vector3 flatOffset = new Vector3(
            Mathf.Sin(orbitRadians) * _orbitRadius,
            0f,
            Mathf.Cos(orbitRadians) * _orbitRadius
        );

        return centerPosition + flatOffset + Vector3.up * _heightOffset;
    }

    private static float NormalizeSignedAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }
        else if (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }
}
