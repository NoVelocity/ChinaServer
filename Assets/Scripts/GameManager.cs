using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] public WindowsManager windowManager;
    [SerializeField] public WindowData mainWindow;
    [SerializeField] public Window mainMenuWindow;

    private void Awake()
    {
        mainMenuWindow = windowManager.CreateWindow(mainWindow, false);
    }
}