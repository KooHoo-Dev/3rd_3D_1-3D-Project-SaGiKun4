using UnityEngine;

namespace Study3D.FpsGame.Map
{
    // Module instances remain editable in the prefab. Batch their static visuals only after
    // the map generator has positioned the room. Colliders and prefab authoring stay separate.
    [DisallowMultipleComponent]
    public sealed class RoomGeometryBatcher : MonoBehaviour
    {
        private void LateUpdate()
        {
            StaticBatchingUtility.Combine(gameObject);
            enabled = false;
        }
    }
}
