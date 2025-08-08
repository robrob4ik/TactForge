
using UnityEngine;

namespace OneBitRob.Constants
{
    [ExecuteInEditMode] // Allows script to run in Edit Mode
    public class SetObjectTiling : MonoBehaviour
    {
        [SerializeField] private Vector2 tiling = new Vector2(1f, 1f); // Manual tiling override
        private Renderer rend;
   
        void OnEnable()
        {
            rend = GetComponent<Renderer>();
            ApplyTiling();
        }

        void ApplyTiling()
        {
            if (rend == null) return;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);

            Vector2 finalTiling = tiling;

            block.SetVector("_BaseMap_ST", new Vector4(finalTiling.x, finalTiling.y, 0f, 0f));
            rend.SetPropertyBlock(block);
        }
    }
}