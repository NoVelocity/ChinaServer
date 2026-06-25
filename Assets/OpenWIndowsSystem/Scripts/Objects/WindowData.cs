using UnityEngine;

[CreateAssetMenu(fileName = "WindowData", menuName = "Open Windows System/Window Data")]
public class WindowData : ScriptableObject
{
    [Header("Service Data")]
    [SerializeField] public bool allowMultipleInstances;
    
    [Header("Title Bar")] [SerializeField] public string title;
    [SerializeField] public bool hideTitleBar;

    [Header("Width")] [SerializeField] public int width;
    [SerializeField] public int minWidth;
    [SerializeField] public int maxWidth;

    [Header("Height")] [SerializeField] public int height;
    [SerializeField] public int minHeight;
    [SerializeField] public int maxHeight;

    [Header("Colors")] [SerializeField] public Color backgroundColor;
    [SerializeField] public Color titleBarColor;
    [SerializeField] public Color titleTextColor;
    
    [Header("Components")]
    [SerializeField] public WindowComponentsController components;
}