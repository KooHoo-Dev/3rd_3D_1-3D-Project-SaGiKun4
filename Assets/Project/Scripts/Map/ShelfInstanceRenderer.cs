using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Study3D.FpsGame.Map
{
    /// <summary>
    /// Instances fixed shelf decorations in spatial cells. Original objects remain editable.
    /// Disable this component to restore normal rendering. Rebuild after editing children at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShelfInstanceRenderer : MonoBehaviour
    {
        // Non-uniform book scales require both object-to-world and inverse matrices (511 limit).
        private const int MaxInstances = 511;
        [SerializeField, Min(1f)] private float cellSize = 16f;
        public int SourceRendererCount { get; private set; }
        public int InstanceBatchCount => batches.Count;

        private sealed class Batch
        {
            public Mesh mesh;
            public RenderParams parameters;
            public MeshRenderer[] sources;
            public Matrix4x4[] matrices;
            public MaterialPropertyBlock properties;
        }

        // 한 번에 같이 그릴 수 있는 렌더러 묶음의 조건 (메시, 머티리얼, 칸, 렌더 설정이 모두 같아야 함)
        private struct GroupKey : IEquatable<GroupKey>
        {
            public Mesh mesh;
            public Material material;
            public Vector3Int cell;
            public int layer;
            public ShadowCastingMode shadowCastingMode;
            public bool receiveShadows;
            public uint renderingLayerMask;
            public ReflectionProbeUsage reflectionProbeUsage;
            public LightProbeUsage lightProbeUsage;
            public MotionVectorGenerationMode motionVectorGenerationMode;

            public bool Equals(GroupKey other)
            {
                return mesh == other.mesh
                    && material == other.material
                    && cell == other.cell
                    && layer == other.layer
                    && shadowCastingMode == other.shadowCastingMode
                    && receiveShadows == other.receiveShadows
                    && renderingLayerMask == other.renderingLayerMask
                    && reflectionProbeUsage == other.reflectionProbeUsage
                    && lightProbeUsage == other.lightProbeUsage
                    && motionVectorGenerationMode == other.motionVectorGenerationMode;
            }

            public override bool Equals(object obj)
            {
                if (obj is GroupKey)
                    return Equals((GroupKey)obj);

                return false;
            }

            public override int GetHashCode()
            {
                int hash = 17;
                hash = hash * 31 + mesh.GetHashCode();
                hash = hash * 31 + material.GetHashCode();
                hash = hash * 31 + cell.GetHashCode();
                hash = hash * 31 + layer;
                hash = hash * 31 + (int)shadowCastingMode;
                hash = hash * 31 + (receiveShadows ? 1 : 0);
                hash = hash * 31 + (int)renderingLayerMask;
                hash = hash * 31 + (int)reflectionProbeUsage;
                hash = hash * 31 + (int)lightProbeUsage;
                hash = hash * 31 + (int)motionVectorGenerationMode;
                return hash;
            }
        }

        private readonly List<Batch> batches = new List<Batch>();
        private readonly List<MeshRenderer> hiddenRenderers = new List<MeshRenderer>();
        private Matrix4x4 rootMatrix;
        private bool initialized;

        private void OnEnable()
        {
            initialized = false;
        }

        private void LateUpdate()
        {
            // Map generation can position a room after Instantiate/Awake. Initialize afterwards.
            if (!initialized)
            {
                Rebuild();
            }
            if (rootMatrix != transform.localToWorldMatrix)
            {
                RefreshMatrices();
            }
            foreach (Batch batch in batches)
            {
                Graphics.RenderMeshInstanced(batch.parameters, batch.mesh, 0, batch.matrices);
            }
        }

        public void Rebuild()
        {
            RestoreRenderers();
            initialized = true;
            if (!Application.isPlaying || !isActiveAndEnabled || !SystemInfo.supportsInstancing)
                return;

            Dictionary<GroupKey, List<MeshRenderer>> groups = GroupRenderers();

            foreach (KeyValuePair<GroupKey, List<MeshRenderer>> pair in groups)
            {
                List<MeshRenderer> list = pair.Value;
                if (list.Count < 2)
                    continue;

                for (int start = 0; start < list.Count; start += MaxInstances)
                {
                    int count = Mathf.Min(MaxInstances, list.Count - start);
                    batches.Add(CreateBatch(list, start, count));
                }
            }

            RefreshMatrices();
            HideSourceRenderers();
            SourceRendererCount = hiddenRenderers.Count;
        }

        // 인스턴싱할 수 있는 렌더러를 같은 조건끼리 묶음
        private Dictionary<GroupKey, List<MeshRenderer>> GroupRenderers()
        {
            Dictionary<GroupKey, List<MeshRenderer>> groups = new Dictionary<GroupKey, List<MeshRenderer>>();
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (CanInstance(renderer, out Mesh mesh, out Material material) == false)
                    continue;

                Vector3 position = transform.InverseTransformPoint(renderer.bounds.center) / Mathf.Max(1f, cellSize);
                Vector3Int cell = new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), Mathf.FloorToInt(position.z));

                GroupKey key = new GroupKey
                {
                    mesh = mesh,
                    material = material,
                    cell = cell,
                    layer = renderer.gameObject.layer,
                    shadowCastingMode = renderer.shadowCastingMode,
                    receiveShadows = renderer.receiveShadows,
                    renderingLayerMask = renderer.renderingLayerMask,
                    reflectionProbeUsage = renderer.reflectionProbeUsage,
                    lightProbeUsage = renderer.lightProbeUsage,
                    motionVectorGenerationMode = renderer.motionVectorGenerationMode,
                };

                if (groups.TryGetValue(key, out List<MeshRenderer> list) == false)
                {
                    list = new List<MeshRenderer>();
                    groups.Add(key, list);
                }
                list.Add(renderer);
            }
            return groups;
        }

        private static bool CanInstance(MeshRenderer renderer, out Mesh mesh, out Material material)
        {
            mesh = null;
            material = null;

            // Keep unsupported, lightmapped, LOD-controlled or customized renderers on their usual path.
            if (!renderer.enabled || renderer.forceRenderingOff || renderer.isPartOfStaticBatch)
                return false;
            if (renderer.HasPropertyBlock() || renderer.GetComponentInParent<LODGroup>() != null)
                return false;
            if (renderer.lightProbeProxyVolumeOverride != null || renderer.probeAnchor != null)
                return false;
            if (renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534)
                return false;

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null)
            {
                mesh = filter.sharedMesh;
            }
            Material[] materials = renderer.sharedMaterials;
            if (mesh == null || mesh.subMeshCount != 1 || materials.Length != 1)
                return false;

            material = materials[0];
            if (material == null || !material.enableInstancing || material.renderQueue >= 2500)
                return false;

            return true;
        }

        private static Batch CreateBatch(List<MeshRenderer> list, int start, int count)
        {
            MeshRenderer sample = list[start];

            // 라이트 프로브 섞기는 직접 계산해서 넘김 (RefreshMatrices 참고)
            LightProbeUsage lightProbeUsage = sample.lightProbeUsage;
            if (lightProbeUsage == LightProbeUsage.BlendProbes)
            {
                lightProbeUsage = LightProbeUsage.CustomProvided;
            }

            Batch batch = new Batch
            {
                mesh = sample.GetComponent<MeshFilter>().sharedMesh,
                sources = list.GetRange(start, count).ToArray(),
                matrices = new Matrix4x4[count],
                properties = new MaterialPropertyBlock(),
                parameters = new RenderParams(sample.sharedMaterial)
                {
                    layer = sample.gameObject.layer,
                    shadowCastingMode = sample.shadowCastingMode,
                    receiveShadows = sample.receiveShadows,
                    renderingLayerMask = sample.renderingLayerMask,
                    reflectionProbeUsage = sample.reflectionProbeUsage,
                    lightProbeUsage = lightProbeUsage,
                    motionVectorMode = sample.motionVectorGenerationMode
                }
            };
            batch.parameters.matProps = batch.properties;
            return batch;
        }

        private void HideSourceRenderers()
        {
            foreach (Batch batch in batches)
            {
                foreach (MeshRenderer renderer in batch.sources)
                {
                    renderer.forceRenderingOff = true;
                    hiddenRenderers.Add(renderer);
                }
            }
        }

        private void RefreshMatrices()
        {
            rootMatrix = transform.localToWorldMatrix;
            foreach (Batch batch in batches)
            {
                Bounds bounds = batch.sources[0].bounds;
                Vector3[] positions = new Vector3[batch.sources.Length];
                for (int i = 0; i < batch.sources.Length; i++)
                {
                    MeshRenderer renderer = batch.sources[i];
                    batch.matrices[i] = renderer.localToWorldMatrix;
                    bounds.Encapsulate(renderer.bounds);
                    positions[i] = renderer.bounds.center;
                }
                batch.parameters.worldBounds = bounds;
                if (batch.parameters.lightProbeUsage == LightProbeUsage.CustomProvided)
                {
                    SphericalHarmonicsL2[] probes = new SphericalHarmonicsL2[positions.Length];
                    Vector4[] occlusion = new Vector4[positions.Length];
                    LightProbes.CalculateInterpolatedLightAndOcclusionProbes(positions, probes, occlusion);
                    batch.properties.CopySHCoefficientArraysFrom(probes);
                    batch.properties.CopyProbeOcclusionArrayFrom(occlusion);
                }
            }
        }

        private void OnDisable()
        {
            RestoreRenderers();
        }

        private void RestoreRenderers()
        {
            foreach (MeshRenderer renderer in hiddenRenderers)
            {
                if (renderer != null)
                {
                    renderer.forceRenderingOff = false;
                }
            }
            hiddenRenderers.Clear();
            batches.Clear();
            SourceRendererCount = 0;
        }
    }
}
