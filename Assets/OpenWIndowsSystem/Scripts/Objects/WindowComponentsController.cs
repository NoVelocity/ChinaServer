using UnityEngine;

[RequireComponent(typeof(Canvas))]
public abstract class WindowComponentsController : MonoBehaviour
{
    public Window Window { get; private set; }

    public void Init(Window window)
    {
        Window = window;
    }

    public abstract bool OnClose(bool isPlayerInput);

    public abstract void OnOpen(bool isPlayerInput);

    public abstract void OnFocus(bool isPlayerInput);

    public abstract void OnUnfocus(bool isPlayerInput);

    public abstract void OnResize(bool isPlayerInput);
}