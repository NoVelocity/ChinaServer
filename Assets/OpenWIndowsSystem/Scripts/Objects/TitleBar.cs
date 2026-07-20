using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TitleBar : MonoBehaviour
{
    public Window Window { get; set; }

    public void OnDrag(BaseEventData eventData)
    {
        Window.GetComponent<RectTransform>().anchoredPosition += ((PointerEventData)eventData).delta;
    }
}