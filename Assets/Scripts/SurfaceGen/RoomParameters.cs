using UnityEngine;

namespace SurfaceGen
{
    public class RoomParameters : ScriptableObject
    {
        public string id;
        public int[] markers = new int[4];
        public Vector3 size; // x, y, z ==> w, h, d
    }
}