using Meta.XR.MRUtilityKit;
using UnityEngine;
using UnityEngine.AI;

namespace CSE165.Project3
{
    [RequireComponent(typeof(OVRHand), typeof(OVRSkeleton))]
    public class HandGestureDestinationSelector : MonoBehaviour
    {
        [SerializeField] private OVRHand hand;
        [SerializeField] private OVRSkeleton skeleton;
        [SerializeField] private Transform destinationMarker;
        [SerializeField] private float maxRayDistance = 8f;
        [SerializeField] private float holdSeconds = 0.2f;
        [SerializeField] private float navMeshSampleRadius = 1.25f;
        [SerializeField] private bool showPointerRay = true;
        [SerializeField] private float pointerWidth = 0.012f;
        [SerializeField] private float pointerMarkerScale = 0.08f;
        [SerializeField] private Color validPointerColor = new(0.1f, 1f, 0.35f, 0.95f);
        [SerializeField] private Color invalidPointerColor = new(1f, 0.2f, 0.05f, 0.75f);

        private readonly LabelFilter floorFilter =
            new(MRUKAnchor.SceneLabels.FLOOR, MRUKAnchor.ComponentType.Plane);

        private float pinchHeldFor;
        private bool firedForPinch;
        private Transform wrist;
        private Transform indexTip;
        private LineRenderer pointerLine;
        private Transform pointerMarker;
        private Material pointerLineMaterial;
        private Material pointerMarkerMaterial;

        private void Awake()
        {
            hand = GetComponent<OVRHand>();
            skeleton = GetComponent<OVRSkeleton>();
            CreatePointerRay();
        }

        private void Update()
        {
            if (!HandIsTracked())
            {
                ResetPinch();
                SetPointerVisible(false);
                return;
            }

            bool hasDestination = TryGetDestination(out var destination, out var ray, out var rayEnd);
            UpdatePointerRay(ray, rayEnd, hasDestination, destination);

            if (!hand.GetFingerIsPinching(OVRHand.HandFinger.Index))
            {
                ResetPinch();
                return;
            }

            pinchHeldFor += Time.deltaTime;
            if (pinchHeldFor < holdSeconds || firedForPinch || !hasDestination)
            {
                return;
            }

            firedForPinch = true;
            destinationMarker.gameObject.SetActive(true);
            destinationMarker.position = destination;
        }

        private bool HandIsTracked()
        {
            return hand.IsTracked && hand.HandConfidence == OVRHand.TrackingConfidence.High;
        }

        private void ResetPinch()
        {
            pinchHeldFor = 0f;
            firedForPinch = false;
        }

        private bool TryGetDestination(out Vector3 destination, out Ray ray, out Vector3 rayEnd)
        {
            destination = default;
            ray = BuildPointingRay();
            rayEnd = ray.origin + ray.direction * maxRayDistance;

            var room = GetCurrentRoom();
            if (room == null)
            {
                return false;
            }

            if (!room.Raycast(ray, maxRayDistance, floorFilter, out var hit, out _))
            {
                return false;
            }

            rayEnd = hit.point;
            if (!NavMesh.SamplePosition(hit.point, out var navHit, navMeshSampleRadius, WalkableAreaMask()))
            {
                return false;
            }

            destination = navHit.position;
            return true;
        }

        private Ray BuildPointingRay()
        {
            if (hand.IsPointerPoseValid)
            {
                return new Ray(hand.PointerPose.position, hand.PointerPose.forward);
            }

            CacheHandJoints();
            if (wrist != null && indexTip != null)
            {
                return new Ray(indexTip.position, (indexTip.position - wrist.position).normalized);
            }

            return new Ray(transform.position, transform.forward);
        }

        private void CacheHandJoints()
        {
            if (wrist != null && indexTip != null)
            {
                return;
            }

            if (!skeleton.IsInitialized || skeleton.Bones == null)
            {
                return;
            }

            foreach (var bone in skeleton.Bones)
            {
                if (bone.Id == OVRSkeleton.BoneId.XRHand_Wrist)
                {
                    wrist = bone.Transform;
                }
                else if (bone.Id == OVRSkeleton.BoneId.XRHand_IndexTip)
                {
                    indexTip = bone.Transform;
                }
            }
        }

        private void CreatePointerRay()
        {
            pointerLineMaterial = CreatePointerMaterial(validPointerColor);
            pointerMarkerMaterial = CreatePointerMaterial(validPointerColor);

            var lineObject = new GameObject("Hand Destination Pointer");
            pointerLine = lineObject.AddComponent<LineRenderer>();
            pointerLine.useWorldSpace = true;
            pointerLine.positionCount = 2;
            pointerLine.widthMultiplier = pointerWidth;
            pointerLine.sharedMaterial = pointerLineMaterial;
            pointerLine.enabled = false;

            var markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerObject.name = "Hand Destination Preview";
            pointerMarker = markerObject.transform;
            pointerMarker.localScale = Vector3.one * pointerMarkerScale;
            Destroy(markerObject.GetComponent<Collider>());
            markerObject.GetComponent<MeshRenderer>().sharedMaterial = pointerMarkerMaterial;
            markerObject.SetActive(false);
        }

        private void UpdatePointerRay(Ray ray, Vector3 rayEnd, bool hasDestination, Vector3 destination)
        {
            if (!showPointerRay)
            {
                SetPointerVisible(false);
                return;
            }

            SetPointerColor(hasDestination ? validPointerColor : invalidPointerColor);
            pointerLine.enabled = true;
            pointerLine.widthMultiplier = pointerWidth;
            pointerLine.SetPosition(0, ray.origin);
            pointerLine.SetPosition(1, rayEnd);

            pointerMarker.gameObject.SetActive(hasDestination);
            if (hasDestination)
            {
                pointerMarker.position = destination + Vector3.up * 0.015f;
            }
        }

        private void SetPointerVisible(bool visible)
        {
            pointerLine.enabled = visible;
            pointerMarker.gameObject.SetActive(visible);
        }

        private void SetPointerColor(Color color)
        {
            pointerLine.startColor = color;
            pointerLine.endColor = color;
            pointerLineMaterial.color = color;
            pointerMarkerMaterial.color = color;

            if (pointerLineMaterial.HasProperty("_BaseColor"))
            {
                pointerLineMaterial.SetColor("_BaseColor", color);
            }

            if (pointerMarkerMaterial.HasProperty("_BaseColor"))
            {
                pointerMarkerMaterial.SetColor("_BaseColor", color);
            }
        }

        private static Material CreatePointerMaterial(Color color)
        {
            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");

            var material = new Material(shader)
            {
                color = color,
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private static MRUKRoom GetCurrentRoom()
        {
            if (MRUK.Instance == null)
            {
                return null;
            }

            return MRUK.Instance.GetCurrentRoom();
        }

        private static int WalkableAreaMask()
        {
            int mask = NavMesh.AllAreas;
            int notWalkableArea = NavMesh.GetAreaFromName("Not Walkable");
            if (notWalkableArea >= 0)
            {
                mask &= ~(1 << notWalkableArea);
            }

            return mask;
        }
    }
}
