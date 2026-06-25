using UnityEngine;

public class MainMenuController : WindowComponentsController
{
    public void QuitButton()
    {
        Window.Close(true);
    }
    public override bool OnClose(bool isPlayerInput)
    {
        Application.Quit();
        return true;
    }

    public override void OnOpen(bool isPlayerInput)
    {
        
    }

    public override void OnFocus(bool isPlayerInput)
    {
        
    }

    public override void OnUnfocus(bool isPlayerInput)
    {
        
    }

    public override void OnResize(bool isPlayerInput)
    {
        
    }
}