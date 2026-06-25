using UnityEngine;

public class WindowsManager : MonoBehaviour
{
    [SerializeField] GameObject genericWindow;
    [SerializeField] Canvas canvas;

    void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    public Window CreateWindow(WindowData data, bool isPlayerInput)
    {
        Window window = Instantiate(genericWindow, canvas.transform).GetComponent<Window>();
        window.WindowData = data;
        window.Build(new Vector2(canvas.pixelRect.width, canvas.pixelRect.height), isPlayerInput);
        return window;
    }
}