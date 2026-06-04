using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class OutsidePanelClickCloser : MonoBehaviour, IPointerClickHandler
{
    private RectTransform _protectedPanel;
    private Action _onOutsideClick;

    public void Initialize(RectTransform protectedPanel, Action onOutsideClick)
    {
        _protectedPanel = protectedPanel;
        _onOutsideClick = onOutsideClick;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (
            _protectedPanel != null
            && RectTransformUtility.RectangleContainsScreenPoint(
                _protectedPanel,
                eventData.position,
                eventData.pressEventCamera
            )
        )
        {
            return;
        }

        _onOutsideClick?.Invoke();
    }
}
