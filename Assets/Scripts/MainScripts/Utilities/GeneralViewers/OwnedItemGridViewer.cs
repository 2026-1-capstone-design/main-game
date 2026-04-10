using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class OwnedItemGridViewer : MonoBehaviour
{
    [Header("Layout References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform viewportRect;
    [SerializeField] private RectTransform containerRect;
    [SerializeField] private GridLayoutGroup gridLayoutGroup;

    [Header("Cell Prefab")]
    [SerializeField] private OwnedItemGridCell cellPrefab;

    [Header("Grid Settings")]
    [SerializeField] private int fixedColumnCount = 6;

    private readonly List<OwnedItemGridCell> _cellPool = new List<OwnedItemGridCell>();
    private int _activeItemCount;
    private Action<OwnedItemViewData> _onCellClicked;

    private RectTransform _rootRect;

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
        _activeItemCount = items != null ? items.Count : 0;

        EnsureCellPool(_activeItemCount);

        for (int i = 0; i < _cellPool.Count; i++)
        {
            bool shouldShow = i < _activeItemCount;
            OwnedItemGridCell cell = _cellPool[i];

            if (shouldShow)
            {
                cell.gameObject.SetActive(true);
                cell.Setup(items[i], OnCellClickedInternal);
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
        ConfigureStaticLayout();
        NormalizeRectTransforms();

        Canvas.ForceUpdateCanvases();

        if (viewportRect == null || containerRect == null || gridLayoutGroup == null)
        {
            Debug.LogError("[OwnedItemGridViewer] Required Rect/UI reference is missing.", this);
            return;
        }

        float viewportWidth = viewportRect.rect.width;
        if (viewportWidth <= 0f)
        {
            return;
        }

        int columnCount = Mathf.Max(1, fixedColumnCount);
        float cellSize = viewportWidth / columnCount;

        gridLayoutGroup.cellSize = new Vector2(cellSize, cellSize);

        int rowCount = _activeItemCount <= 0
            ? 0
            : Mathf.CeilToInt((float)_activeItemCount / columnCount);

        float contentHeight = rowCount * cellSize;

        containerRect.sizeDelta = new Vector2(0f, contentHeight);

        LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
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
            gridLayoutGroup.spacing = Vector2.zero;
            gridLayoutGroup.padding = new RectOffset(0, 0, 0, 0);
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

        if (_rootRect != null)
        {
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.pivot = new Vector2(0.5f, 0.5f);
            _rootRect.offsetMin = Vector2.zero;
            _rootRect.offsetMax = Vector2.zero;
            _rootRect.localScale = Vector3.one;
            _rootRect.localRotation = Quaternion.identity;
            _rootRect.localPosition = Vector3.zero;
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
            viewportRect.localPosition = Vector3.zero;
        }

        if (containerRect != null)
        {
            containerRect.anchorMin = new Vector2(0f, 1f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.pivot = new Vector2(0.5f, 1f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.localScale = Vector3.one;
            containerRect.localRotation = Quaternion.identity;
            containerRect.localPosition = Vector3.zero;

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
