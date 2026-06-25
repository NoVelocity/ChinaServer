using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Window : MonoBehaviour
{
    // Inspector
    [Header("Window Objects")] [SerializeField]
    private TitleBar titleBar;

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private GameObject closeButton;

    [Header("Window Data")] [SerializeField]
    private WindowData windowData;

    // Service objects
    private Button _closeButtonController;
    private GameObject _thisWindow;
    private WindowComponentsController _componentsController;

    public WindowData WindowData
    {
        get => windowData;
        set => windowData = value;
    }

    private void Awake()
    {
        _thisWindow = gameObject;
        _closeButtonController = closeButton.GetComponent<Button>();
    }

    public void Build(Vector2 canvasSize, bool isPlayerInput)
    {
        CalculatePosition(canvasSize, new Vector2(canvasSize.x / 2f, canvasSize.y / 2f));

        titleBar.gameObject.SetActive(!windowData.hideTitleBar);
        titleBar.GetComponent<Image>().color = windowData.titleBarColor;

        titleText.text = windowData.title;
        titleText.color = windowData.titleTextColor;

        _thisWindow.GetComponent<Image>().color = windowData.backgroundColor;

        _closeButtonController.onClick.AddListener(() => Close(true));

        _componentsController = Instantiate(windowData.components.gameObject, _thisWindow.transform)
            .GetComponent<WindowComponentsController>();
        _componentsController.Init(this);
        
        _componentsController.OnOpen(isPlayerInput);
    }

    public void CalculatePosition(Vector2 canvasSize, Vector2 newPosition)
    {
        RectTransform rect = _thisWindow.GetComponent<RectTransform>();

        float marginLeftRight = (canvasSize.x - windowData.width),
            marginTopBottom = (canvasSize.y - windowData.height);

        rect.offsetMin = new Vector2(0, marginTopBottom);
        rect.offsetMax = new Vector2(-marginLeftRight, 0);
        print(rect.position);
    }

    public void Close(bool isPlayerInput, bool forceClose = false)
    {
        bool isCanceled = !_componentsController.OnClose(isPlayerInput);
        if (!isCanceled)
        {
            Destroy(_thisWindow);
        }
    }

    public void Focus(bool force = false)
    {
    }

    public void Resize(int width, int height, bool force = false)
    {
    }
}