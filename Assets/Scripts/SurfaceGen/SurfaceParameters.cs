using UnityEngine;

[CreateAssetMenu(menuName = "SurfaceGen/SurfaceParameters")]
public class SurfaceParameters : ScriptableObject
{
    public Vector3 scale;
    public float wallScale = 0.5f;
    public WallParameters[] wallParameters;
}