using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace CSE165.Project3
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class AgentNavigationController : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform destinationMarker;
        [SerializeField] private float destinationSampleRadius = 1.25f;
        [SerializeField] private float repathDistance = 0.1f;
        [SerializeField] private string walkingParameter = "Walking";
        [SerializeField] private float initialDistanceFromUser = 0.9f;
        [SerializeField] private float initialSideOffset = 0.25f;
        [SerializeField] private float initialPlacementSampleRadius = 1.75f;

        private Vector3 lastDestination = new(float.PositiveInfinity, 0f, 0f);
        private int walkingHash;

        private void Reset()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            walkingHash = Animator.StringToHash(walkingParameter);
        }

        private IEnumerator Start()
        {
            for (int frame = 0; frame < 600; frame++)
            {
                if (TryPlaceNearUser())
                {
                    yield break;
                }

                yield return null;
            }
        }

        private void Update()
        {
            if (destinationMarker != null && destinationMarker.gameObject.activeInHierarchy)
            {
                MoveTo(destinationMarker.position);
            }

            animator.SetBool(walkingHash, agent.velocity.magnitude > 0.05f && !agent.isStopped);
        }

        private void MoveTo(Vector3 worldPosition)
        {
            if (Vector3.Distance(lastDestination, worldPosition) < repathDistance)
            {
                return;
            }

            if (!agent.isOnNavMesh)
            {
                TryPlaceOnNavMesh();
            }

            if (!agent.isOnNavMesh)
            {
                return;
            }

            if (NavMesh.SamplePosition(worldPosition, out var hit, destinationSampleRadius, WalkableAreaMask()))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
                lastDestination = worldPosition;
            }
        }

        private void TryPlaceOnNavMesh()
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, destinationSampleRadius, WalkableAreaMask()))
            {
                agent.Warp(hit.position);
            }
        }

        private bool TryPlaceNearUser()
        {
            var userCamera = Camera.main;
            if (userCamera == null)
            {
                return false;
            }

            var forward = Vector3.ProjectOnPlane(userCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var candidate = userCamera.transform.position + forward * initialDistanceFromUser + right * initialSideOffset;

            if (!NavMesh.SamplePosition(candidate, out var hit, initialPlacementSampleRadius, WalkableAreaMask()))
            {
                return false;
            }

            agent.Warp(hit.position);

            var lookAtUser = Vector3.ProjectOnPlane(userCamera.transform.position - transform.position, Vector3.up);
            if (lookAtUser.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(lookAtUser.normalized, Vector3.up);
            }

            lastDestination = new Vector3(float.PositiveInfinity, 0f, 0f);
            return true;
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
