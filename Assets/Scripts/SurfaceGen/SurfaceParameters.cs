using UnityEngine;

namespace SurfaceGen
{
    [CreateAssetMenu(menuName = "SurfaceGen/Surface Parameters")]
    public class SurfaceParameters : ScriptableObject
    {
        public Vector3 scale;
        public WallParameters[] wallParameters;
        public RobotParameters[] robotParameters;
        public RoomParameters[] roomParameters;
    }
}