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

        private readonly List<Batch> batches = new List<Batch>();
        private readonly List<MeshRenderer> hiddenRenderers = new List<MeshRenderer>();
        private Matrix4x4 rootMatrix;
        private bool initialized;

        private void OnEnable() { initialized = false; }

        private void LateUpdate()
        {
            // Map generation can position a room after Instantiate/Awake. Initialize afterwards.
            if (!initialized) Rebuild();
            if (rootMatrix != transform.localToWorldMatrix) RefreshMatrices();
            foreach (var batch in batches)
                Graphics.RenderMeshInstanced(batch.parameters, batch.mesh, 0, batch.matrices);
        }

        [ContextMenu("Rebuild shelf instances (Play Mode)")]
        public void Rebuild()
        {
            RestoreRenderers();
            initialized = true;
            if (!Application.isPlaying || !isActiveAndEnabled || !SystemInfo.supportsInstancing) return;

            // Keep unsupported, lightmapped, LOD-controlled or customized renderers on their usual path.
            var groups = new Dictionary<(Mesh, Material, Vector3Int, int, ShadowCastingMode,
                bool, uint, ReflectionProbeUsage, LightProbeUsage, MotionVectorGenerationMode), List<MeshRenderer>>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || renderer.forceRenderingOff || renderer.isPartOfStaticBatch ||
                    renderer.HasPropertyBlock() || renderer.GetComponentInParent<LODGroup>() != null ||
                    renderer.lightProbeProxyVolumeOverride != null || renderer.probeAnchor != null ||
                    (renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                var materials = renderer.sharedMaterials;
                if (mesh == null || mesh.subMeshCount != 1 || materials.Length != 1) continue;
                var material = materials[0];
                if (material == null || !material.enableInstancing || material.renderQueue >= 2500) continue;

                var position = transform.InverseTransformPoint(renderer.bounds.center) / Mathf.Max(1f, cellSize);
                var cell = new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), Mathf.FloorToInt(position.z));
                var key = (mesh, material, cell, renderer.gameObject.layer, renderer.shadowCastingMode,
                    renderer.receiveShadows, renderer.renderingLayerMask, renderer.reflectionProbeUsage,
                    renderer.lightProbeUsage, renderer.motionVectorGenerationMode);
                if (!groups.TryGetValue(key, out var list)) groups.Add(key, list = new List<MeshRenderer>());
                list.Add(renderer);
            }

            foreach (var pair in groups)
            {
                var list = pair.Value;
                if (list.Count < 2) continue;
                for (int start = 0; start < list.Count; start += MaxInstances)
                {
                    int count = Mathf.Min(MaxInstances, list.Count - start);
                    var sample = list[start];
                    var batch = new Batch
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
                            lightProbeUsage = sample.lightProbeUsage == LightProbeUsage.BlendProbes
                                ? LightProbeUsage.CustomProvided : sample.lightProbeUsage,
                            motionVectorMode = sample.motionVectorGenerationMode
                        }
                    };
                    batch.parameters.matProps = batch.properties;
                    batches.Add(batch);
                }
            }

            RefreshMatrices();
            foreach (var batch in batches)
                foreach (var renderer in batch.sources)
                {
                    renderer.forceRenderingOff = true;
                    hiddenRenderers.Add(renderer);
                }
            SourceRendererCount = hiddenRenderers.Count;
        }

        private void RefreshMatrices()
        {
            rootMatrix = transform.localToWorldMatrix;
            foreach (var batch in batches)
            {
                var bounds = batch.sources[0].bounds;
                var positions = new Vector3[batch.sources.Length];
                for (int i = 0; i < batch.sources.Length; i++)
                {
                    var renderer = batch.sources[i];
                    batch.matrices[i] = renderer.localToWorldMatrix;
                    bounds.Encapsulate(renderer.bounds);
                    positions[i] = renderer.bounds.center;
                }
                batch.parameters.worldBounds = bounds;
                if (batch.parameters.lightProbeUsage == LightProbeUsage.CustomProvided)
                {
                    var probes = new SphericalHarmonicsL2[positions.Length];
                    var occlusion = new Vector4[positions.Length];
                    LightProbes.CalculateInterpolatedLightAndOcclusionProbes(positions, probes, occlusion);
                    batch.properties.CopySHCoefficientArraysFrom(probes);
                    batch.properties.CopyProbeOcclusionArrayFrom(occlusion);
                }
            }
        }

        private void OnDisable() { RestoreRenderers(); }

        private void RestoreRenderers()
        {
            foreach (var renderer in hiddenRenderers)
                if (renderer != null) renderer.forceRenderingOff = false;
            hiddenRenderers.Clear();
            batches.Clear();
            SourceRendererCount = 0;
        }
    }
}
