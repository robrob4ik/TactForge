using UnityEngine;
using Sirenix.OdinInspector;

namespace OneBitRob.Constants
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Renderer))]
    public class SetObjectTiling : MonoBehaviour
    {
        [SerializeField] private Vector2 tiling = new Vector2(1f, 1f);
        private Renderer rend;

        void OnEnable()
        {
            rend = GetComponent<Renderer>();
            ApplyTiling();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ApplyTiling();
            }
        }
#endif

        [Button("Apply Tiling")]
        public void ApplyTiling()
        {
            if (rend == null)
                rend = GetComponent<Renderer>();

            if (rend == null) return;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);

            block.SetVector("_BaseMap_ST", new Vector4(tiling.x, tiling.y, 0f, 0f));
            rend.SetPropertyBlock(block);
        }
    }
}