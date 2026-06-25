using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class TitleBar : MonoBehaviour
{
    public Window Window { get; set; }

    private void OnMouseDrag()
    {
        print(Mouse.current.position.x + " " + Mouse.current.position.y);
    }
}