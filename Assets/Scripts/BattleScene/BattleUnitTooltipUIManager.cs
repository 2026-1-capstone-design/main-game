using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// BattleScene의 3D 전투 유닛 hover tooltip을 관리한다.
// MainScene BattlePanel의 툴팁과 같은 UI 구성을 쓰되, 감지는 UI pointer가 아니라 월드 모델 raycast로 처리한다.
[DisallowMultipleComponent]
public sealed class BattleUnitTooltipUIManager : MonoBehaviour
{
    [Header("Hover")]
    [SerializeField]
    private Camera raycastCamera;

    [SerializeField]
    private LayerMask unitLayerMask = ~0;

    [SerializeField]
    [Min(1f)]
    private float maxRaycastDistance = 500f;

    [SerializeField]
    [Min(0.1f)]
    private float fallbackHoverColliderHeight = 2.4f;

    [SerializeField]
    [Min(0.05f)]
    private float fallbackHoverColliderRadius = 0.6f;

    [Header("Tooltip")]
    [SerializeField]
    private RectTransform tooltipRoot;

    [SerializeField]
    private Vector2 tooltipOffset = new Vector2(16f, -8f);

    [SerializeField]
    private RawImage tooltipIcon;

    [SerializeField]
    private GladiatorModelPreviewView tooltipModelPreviewView;

    [SerializeField]
    private TMP_Text levelText;

    [SerializeField]
    private RawImage levelIcon;

    [SerializeField]
    private TMP_Text attackText;

    [SerializeField]
    private RawImage attackIcon;

    [SerializeField]
    private TMP_Text healthText;

    [SerializeField]
    private RawImage healthIcon;

    [SerializeField]
    private TMP_Text attackSpeedText;

    [SerializeField]
    private RawImage attackSpeedIcon;

    [SerializeField]
    private TMP_Text moveSpeedText;

    [SerializeField]
    private RawImage moveSpeedIcon;

    [SerializeField]
    private TMP_Text rangeText;

    [SerializeField]
    private RawImage rangeIcon;

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog;

    private readonly List<BattleRuntimeUnit> _runtimeUnits = new List<BattleRuntimeUnit>();
    private BattleRuntimeUnit _hoveredUnit;
    private bool _initialized;

    private void Awake()
    {
        HideTooltip();
    }

    private void Update()
    {
        if (!_initialized)
        {
            return;
        }

        BattleRuntimeUnit hoveredUnit = ResolveHoveredUnit();
        if (hoveredUnit != _hoveredUnit)
        {
            if (hoveredUnit != null)
            {
                ShowTooltip(hoveredUnit);
            }
            else
            {
                HideTooltip();
            }
        }

        if (_hoveredUnit != null)
        {
            RefreshTooltipStats(_hoveredUnit);
            UpdateTooltipPosition();
        }
    }

    public void Initialize(IReadOnlyList<BattleRuntimeUnit> runtimeUnits)
    {
        _runtimeUnits.Clear();

        if (runtimeUnits != null)
        {
            for (int i = 0; i < runtimeUnits.Count; i++)
            {
                BattleRuntimeUnit unit = runtimeUnits[i];
                if (unit == null)
                {
                    continue;
                }

                _runtimeUnits.Add(unit);
                EnsureHoverCollider(unit);
            }
        }

        if (tooltipModelPreviewView == null && tooltipIcon != null)
        {
            tooltipModelPreviewView = tooltipIcon.GetComponentInChildren<GladiatorModelPreviewView>(true);
        }

        _hoveredUnit = null;
        _initialized = true;
        HideTooltip();
    }

    public void Clear()
    {
        _runtimeUnits.Clear();
        _initialized = false;
        HideTooltip();
    }

    private BattleRuntimeUnit ResolveHoveredUnit()
    {
        if (Mouse.current == null)
        {
            return null;
        }

        Camera camera = ResolveRaycastCamera();
        if (camera == null)
        {
            return null;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Ray ray = camera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRaycastDistance, unitLayerMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            BattleRuntimeUnit unit = hitCollider != null ? hitCollider.GetComponentInParent<BattleRuntimeUnit>() : null;
            if (unit != null)
            {
                return unit;
            }
        }

        return null;
    }

    private Camera ResolveRaycastCamera()
    {
        if (raycastCamera != null)
        {
            return raycastCamera;
        }

        raycastCamera = Camera.main;
        return raycastCamera;
    }

    private void ShowTooltip(BattleRuntimeUnit unit)
    {
        _hoveredUnit = unit;

        if (tooltipRoot == null)
        {
            if (verboseLog)
            {
                Debug.LogWarning("[BattleUnitTooltipUIManager] Tooltip Root is not assigned.", this);
            }

            return;
        }

        SetUnitPreview(unit);
        RefreshTooltipStats(unit);
        SetTooltipStatIconsActive(true);
        tooltipRoot.gameObject.SetActive(true);
        tooltipRoot.SetAsLastSibling();
        UpdateTooltipPosition();
    }

    private void HideTooltip()
    {
        _hoveredUnit = null;

        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(false);
        }

        if (tooltipModelPreviewView != null)
        {
            tooltipModelPreviewView.Clear();
        }

        if (
            tooltipIcon != null
            && (tooltipModelPreviewView == null || !tooltipModelPreviewView.UsesTargetImage(tooltipIcon))
        )
        {
            tooltipIcon.texture = null;
            tooltipIcon.enabled = false;
        }
    }

    private void SetUnitPreview(BattleRuntimeUnit unit)
    {
        BattleUnitSnapshot snapshot = unit != null ? unit.Snapshot : null;
        GameObject modelPrefab =
            snapshot != null && snapshot.GladiatorClass != null ? snapshot.GladiatorClass.previewModelPrefab : null;
        bool useModelPreview = tooltipModelPreviewView != null && modelPrefab != null;

        if (tooltipModelPreviewView != null)
        {
            if (useModelPreview)
            {
                tooltipModelPreviewView.Show(
                    modelPrefab,
                    snapshot.CustomizeIndicates,
                    snapshot.LeftWeaponPrefab,
                    snapshot.RightWeaponPrefab
                );
            }
            else
            {
                tooltipModelPreviewView.Clear();
            }
        }

        if (tooltipIcon == null)
        {
            return;
        }

        if (useModelPreview)
        {
            if (!tooltipModelPreviewView.UsesTargetImage(tooltipIcon))
            {
                tooltipIcon.texture = null;
                tooltipIcon.enabled = false;
            }

            return;
        }

        Sprite fallbackSprite = snapshot != null ? snapshot.PortraitSprite : null;
        if (fallbackSprite == null || fallbackSprite.texture == null)
        {
            tooltipIcon.texture = null;
            tooltipIcon.enabled = false;
            return;
        }

        Rect textureRect = fallbackSprite.textureRect;
        tooltipIcon.texture = fallbackSprite.texture;
        tooltipIcon.uvRect = new Rect(
            textureRect.x / fallbackSprite.texture.width,
            textureRect.y / fallbackSprite.texture.height,
            textureRect.width / fallbackSprite.texture.width,
            textureRect.height / fallbackSprite.texture.height
        );
        tooltipIcon.enabled = true;
    }

    private void RefreshTooltipStats(BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        SetText(levelText, unit.Level.ToString());
        SetText(attackText, FormatStat(unit.Attack));
        SetText(healthText, FormatHealth(unit.CurrentHealth));
        SetText(attackSpeedText, FormatStat(unit.AttackSpeed));
        SetText(moveSpeedText, FormatStat(unit.MoveSpeed));
        SetText(rangeText, FormatStat(unit.AttackRange));
    }

    private void UpdateTooltipPosition()
    {
        if (tooltipRoot == null || Mouse.current == null)
        {
            return;
        }

        RectTransform parentRect = tooltipRoot.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        Canvas canvas = tooltipRoot.GetComponentInParent<Canvas>();
        Camera eventCamera =
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screenPosition = Mouse.current.position.ReadValue();

        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPosition,
                eventCamera,
                out Vector2 localPosition
            )
        )
        {
            return;
        }

        Vector2 tooltipSize = tooltipRoot.rect.size;
        Rect parentBounds = parentRect.rect;
        tooltipRoot.anchorMin = new Vector2(0f, 1f);
        tooltipRoot.anchorMax = new Vector2(0f, 1f);
        tooltipRoot.pivot = new Vector2(0f, 1f);

        Vector2 anchoredPosition =
            new Vector2(localPosition.x - parentBounds.xMin, localPosition.y - parentBounds.yMax) + tooltipOffset;

        float minX = 0f;
        float maxX = Mathf.Max(0f, parentBounds.width - tooltipSize.x);
        float minY = -Mathf.Max(0f, parentBounds.height - tooltipSize.y);
        float maxY = 0f;

        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);
        tooltipRoot.anchoredPosition = anchoredPosition;
    }

    private void SetTooltipStatIconsActive(bool value)
    {
        SetComponentActive(levelIcon, value);
        SetComponentActive(attackIcon, value);
        SetComponentActive(healthIcon, value);
        SetComponentActive(attackSpeedIcon, value);
        SetComponentActive(moveSpeedIcon, value);
        SetComponentActive(rangeIcon, value);
    }

    private void EnsureHoverCollider(BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        Collider existingCollider = unit.GetComponent<Collider>();
        if (existingCollider != null)
        {
            return;
        }

        CapsuleCollider collider = unit.gameObject.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.radius = Mathf.Max(fallbackHoverColliderRadius, unit.BodyRadius);
        collider.height = Mathf.Max(fallbackHoverColliderHeight, collider.radius * 2f);
        collider.center = new Vector3(0f, collider.height * 0.5f, 0f);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static void SetComponentActive(Behaviour component, bool value)
    {
        if (component != null)
        {
            component.gameObject.SetActive(value);
        }
    }

    private static string FormatStat(float value)
    {
        return value.ToString("0.#");
    }

    private static string FormatHealth(float value)
    {
        return Mathf.RoundToInt(value).ToString();
    }
}
