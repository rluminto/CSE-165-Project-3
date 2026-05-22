using UnityEngine;

namespace CSE165.Project3
{
    [RequireComponent(typeof(OVRPassthroughLayer))]
    public class PassthroughBootstrap : MonoBehaviour
    {
        [SerializeField] private OVRPassthroughLayer passthroughLayer;
        [SerializeField] private float opacity = 1f;

        private void Awake()
        {
            passthroughLayer = GetComponent<OVRPassthroughLayer>();
            ConfigureTransparentCameras();
            ConfigurePassthroughLayer();
        }

        private void Start()
        {
            if (!OVRManager.IsInsightPassthroughSupported())
            {
                Debug.LogError("Passthrough is not supported by this runtime/device.");
                return;
            }

            OVRManager.instance.isInsightPassthroughEnabled = true;
            ConfigurePassthroughLayer();
        }

        private void ConfigureTransparentCameras()
        {
            foreach (var sceneCamera in GetComponentsInChildren<Camera>(true))
            {
                sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                sceneCamera.backgroundColor = Color.clear;
            }
        }

        private void ConfigurePassthroughLayer()
        {
#pragma warning disable CS0618
            passthroughLayer.overlayType = OVROverlay.OverlayType.Underlay;
            passthroughLayer.projectionSurfaceType = OVRPassthroughLayer.ProjectionSurfaceType.Reconstructed;
            passthroughLayer.compositionDepth = 0;
#pragma warning restore CS0618
            passthroughLayer.hidden = false;
            passthroughLayer.enabled = true;
            passthroughLayer.textureOpacity = opacity;
            passthroughLayer.SetStyleDirty();
        }
    }
}
