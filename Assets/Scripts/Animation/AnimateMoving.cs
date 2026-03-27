using UnityEngine;

namespace Animation
{
    public class AnimateMoving : MonoBehaviour
    {
        [Header("Offset Animation")]
        [SerializeField] private Vector2 offsetAxis = Vector2.right;
        [SerializeField] private float offsetSpeed = 0.2f;
        [SerializeField] private string texturePropertyName = "_BaseMap";

        private MeshRenderer _meshRenderer;
        private Material _runtimeMaterial;
        private Vector2 _currentOffset;
        private int _resolvedTexturePropertyId = -1;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private void Start()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
            {
                Debug.LogWarning("AnimateMoving: MeshRenderer not found.", this);
                enabled = false;
                return;
            }

            // Renderer.material always targets the first material slot instance.
            _runtimeMaterial = _meshRenderer.material;
            if (_runtimeMaterial == null)
            {
                Debug.LogWarning("AnimateMoving: First material not found on MeshRenderer.", this);
                enabled = false;
                return;
            }

            _resolvedTexturePropertyId = ResolveTexturePropertyId(_runtimeMaterial, texturePropertyName);
            if (_resolvedTexturePropertyId < 0)
            {
                Debug.LogWarning("AnimateMoving: No compatible texture property found for offset animation.", this);
                enabled = false;
                return;
            }

            _currentOffset = _runtimeMaterial.GetTextureOffset(_resolvedTexturePropertyId);
        }

        private void Update()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            _currentOffset += offsetAxis * (offsetSpeed * Time.deltaTime);
            _currentOffset.x = Mathf.Repeat(_currentOffset.x, 1f);
            _currentOffset.y = Mathf.Repeat(_currentOffset.y, 1f);
            _runtimeMaterial.SetTextureOffset(_resolvedTexturePropertyId, _currentOffset);
        }

        private static int ResolveTexturePropertyId(Material material, string preferredProperty)
        {
            if (material == null)
            {
                return -1;
            }

            // URP Lit uses _BaseMap for the visible albedo transform.
            if (material.HasProperty(BaseMapId))
            {
                return BaseMapId;
            }

            if (!string.IsNullOrWhiteSpace(preferredProperty))
            {
                int preferredId = Shader.PropertyToID(preferredProperty);
                if (material.HasProperty(preferredId))
                {
                    return preferredId;
                }
            }

            if (material.HasProperty(MainTexId))
            {
                return MainTexId;
            }

            return -1;
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
            }
        }
    }
}
