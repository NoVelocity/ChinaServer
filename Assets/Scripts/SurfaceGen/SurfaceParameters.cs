using UnityEngine;

namespace SurfaceGen
{
    [CreateAssetMenu(menuName = "SurfaceGen/Surface Parameters")]
    public class SurfaceParameters : ScriptableObject
    {
        public Vector3 scale;
        public float wallScale = 0.5f;
        public WallParameters[] wallParameters;
    }
}