using System;
using UnityEngine;

public class WallRender : MonoBehaviour
{
    public Color wallColor;
    private Renderer[] _renderers;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        foreach (var render in _renderers)
        {
            render.material.color = wallColor;
        }
    }
}
