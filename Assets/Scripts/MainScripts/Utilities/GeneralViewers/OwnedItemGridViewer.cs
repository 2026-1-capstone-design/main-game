using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class OwnedItemGridViewer : MonoBehaviour
{
    [Header("Layout References")]
    [SerializeField]
    private ScrollRect scrollRect;

    [SerializeField]
    private RectTransform viewportRect;

    [SerializeField]
    private RectTransform containerRect;

    [SerializeField]
    private GridLayoutGroup gridLayoutGroup;

    [Header("Cell Prefab")]
    [SerializeField]
    private OwnedItemGridCell cellPrefab;

    [Header("Grid Settings")]
    [SerializeField]
    private int fixedColumnCount = 6;

    [SerializeField]
    private Vector2 cellSpacing = Vector2.zero;

    [SerializeField]
    private int paddingLeft;

    [SerializeField]
    private int paddingRight;

    [SerializeField]
    private int paddingTop;

    [SerializeField]
    private int paddingBottom;

    [SerializeField]
    private bool stretchRootToParent = true;

    [Header("Empty Cells")]
    [SerializeField]
    private int minimumVisibleCellCount;

    [SerializeField]
    private Texture emptyCellRawIcon;

    [SerializeField]
    private bool useMaxCellSize;

    [SerializeField]
    private float maxCellSize = 120f;

    private readonly List<OwnedItemGridCell> _cellPool = new List<OwnedItemGridCell>();
    private int _activeItemCount;
    private Action<OwnedItemViewData> _onCellClicked;

    private RectTransform _rootRect;
    private bool _isRefreshingLayout;

    private void Awake()
    {
        _rootRect = GetComponent<RectTransform>();
        ConfigureStaticLayout();
        NormalizeRectTransforms();
    }

    private void OnEnable()
    {
        RefreshLayoutNow();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        RefreshLayoutNow();
    }

    public void SetItems(IReadOnlyList<OwnedItemViewData> items, Action<OwnedItemViewData> onCellClicked)
    {
        ConfigureStaticLayout();
        NormalizeRectTransforms();

        _onCellClicked = onCellClicked;
        int itemCount = items != null ? items.Count : 0;
        _activeItemCount = Mathf.Max(itemCount, Mathf.Max(0, minimumVisibleCellCount));

        EnsureCellPool(_activeItemCount);

        for (int i = 0; i < _cellPool.Count; i++)
        {
            bool shouldShow = i < _activeItemCount;
            OwnedItemGridCell cell = _cellPool[i];

            if (shouldShow)
            {
                cell.gameObject.SetActive(true);
                OwnedItemViewData data = i < itemCount ? items[i] : OwnedItemViewData.Placeholder(emptyCellRawIcon);
                cell.Setup(data, OnCellClickedInternal);
            }
            else
            {
                cell.Clear();
                cell.gameObject.SetActive(false);
            }
        }

        RefreshLayoutNow();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void ClearAll()
    {
        _activeItemCount = 0;
        _onCellClicked = null;

        for (int i = 0; i < _cellPool.Count; i++)
        {
            _cellPool[i].Clear();
            _cellPool[i].gameObject.SetActive(false);
        }

        RefreshLayoutNow();
    }

    public void RefreshLayoutNow()
    {
        if (_isRefreshingLayout)
        {
            return;
        }

        _isRefreshingLayout = true;

        ConfigureStaticLayout();
        NormalizeRectTransforms();

        Canvas.ForceUpdateCanvases();

        if (viewportRect == null || containerRect == null || gridLayoutGroup == null)
        {
            Debug.LogError("[OwnedItemGridViewer] Required Rect/UI reference is missing.", this);
            _isRefreshingLayout = false;
            return;
        }

        float viewportWidth = viewportRect.rect.width;
        if (viewportWidth <= 0f)
        {
            _isRefreshingLayout = false;
            return;
        }

        int columnCount = Mathf.Max(1, fixedColumnCount);
        Vector2 spacing = gridLayoutGroup.spacing;
        RectOffset padding = gridLayoutGroup.padding;
        float availableWidth = viewportWidth - padding.left - padding.right - spacing.x * Mathf.Max(0, columnCount - 1);
        float cellSize = Mathf.Max(1f, availableWidth) / columnCount;
        if (useMaxCellSize)
        {
            cellSize = Mathf.Min(cellSize, Mathf.Max(1f, maxCellSize));
        }

        gridLayoutGroup.cellSize = new Vector2(cellSize, cellSize);

        int rowCount = _activeItemCount <= 0 ? 0 : Mathf.CeilToInt((float)_activeItemCount / columnCount);

        float contentHeight = padding.top + padding.bottom;
        if (rowCount > 0)
        {
            contentHeight += rowCount * cellSize + spacing.y * Mathf.Max(0, rowCount - 1);
        }

        Vector2 targetSize = new Vector2(0f, contentHeight);
        if (containerRect.sizeDelta != targetSize)
        {
            containerRect.sizeDelta = targetSize;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);

        _isRefreshingLayout = false;
    }

    private void ConfigureStaticLayout()
    {
        if (scrollRect != null)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        if (gridLayoutGroup != null)
        {
            gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayoutGroup.constraintCount = Mathf.Max(1, fixedColumnCount);
            gridLayoutGroup.spacing = cellSpacing;
            gridLayoutGroup.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);
            gridLayoutGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
        }
    }

    private void NormalizeRectTransforms()
    {
        if (_rootRect == null)
        {
            _rootRect = GetComponent<RectTransform>();
        }

        if (_rootRect != null && stretchRootToParent)
        {
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.pivot = new Vector2(0.5f, 0.5f);
            _rootRect.offsetMin = Vector2.zero;
            _rootRect.offsetMax = Vector2.zero;
            _rootRect.localScale = Vector3.one;
            _rootRect.localRotation = Quaternion.identity;
        }

        if (viewportRect != null)
        {
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportRect.localScale = Vector3.one;
            viewportRect.localRotation = Quaternion.identity;
        }

        if (containerRect != null)
        {
            containerRect.anchorMin = new Vector2(0f, 1f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.pivot = new Vector2(0.5f, 1f);
            containerRect.offsetMin = new Vector2(0f, containerRect.offsetMin.y);
            containerRect.offsetMax = new Vector2(0f, containerRect.offsetMax.y);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.localScale = Vector3.one;
            containerRect.localRotation = Quaternion.identity;

            Vector2 sizeDelta = containerRect.sizeDelta;
            containerRect.sizeDelta = new Vector2(0f, sizeDelta.y);
        }
    }

    private void EnsureCellPool(int requiredCount)
    {
        if (cellPrefab == null)
        {
            Debug.LogError("[OwnedItemGridViewer] cellPrefab is not assigned.", this);
            return;
        }

        if (containerRect == null)
        {
            Debug.LogError("[OwnedItemGridViewer] containerRect is not assigned.", this);
            return;
        }

        while (_cellPool.Count < requiredCount)
        {
            OwnedItemGridCell newCell = Instantiate(cellPrefab, containerRect);
            newCell.gameObject.SetActive(false);
            _cellPool.Add(newCell);
        }
    }

    private void OnCellClickedInternal(OwnedItemViewData data)
    {
        _onCellClicked?.Invoke(data);
    }
}
