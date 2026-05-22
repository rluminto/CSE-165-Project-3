using System.Collections;
using Meta.XR.MRUtilityKit;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace CSE165.Project3
{
    [RequireComponent(typeof(NavMeshSurface))]
    public class RoomSurfaceManager : MonoBehaviour
    {
        [SerializeField] private Transform surfaceRoot;
        [SerializeField] private NavMeshSurface navMeshSurface;
        [SerializeField] private string surfaceLayerName = "Surface";
        [SerializeField] private bool showSurfaceOutlines = true;
        [SerializeField] private float outlineOffset = 0.025f;
        [SerializeField] private float outlineWidth = 0.012f;
        [SerializeField] private float wallColliderThickness = 0.06f;
        [SerializeField] private Color floorColor = new(0f, 0.8f, 0.35f, 1f);
        [SerializeField] private Color wallColor = new(1f, 0.35f, 0.1f, 1f);

        private int surfaceLayer;
        private bool builtRoom;
        private Material outlineMaterial;

        private void Reset()
        {
            surfaceRoot = transform;
            navMeshSurface = GetComponent<NavMeshSurface>();
        }

        private void Awake()
        {
            surfaceRoot ??= transform;
            navMeshSurface = GetComponent<NavMeshSurface>();
            surfaceLayer = LayerMask.NameToLayer(surfaceLayerName);
        }

        private void OnEnable()
        {
            StartCoroutine(WaitForMRUKRoom());
        }

        private void OnDisable()
        {
            StopAllCoroutines();

            if (MRUK.Instance != null)
            {
                MRUK.Instance.SceneLoadedEvent.RemoveListener(OnSceneLoaded);
            }
        }

        private IEnumerator WaitForMRUKRoom()
        {
            while (MRUK.Instance == null)
            {
                yield return null;
            }

            MRUK.Instance.SceneLoadedEvent.AddListener(OnSceneLoaded);

            for (int frame = 0; frame < 600 && !builtRoom; frame++)
            {
                var room = GetCurrentRoom();
                if (room != null)
                {
                    BuildFromRoom(room);
                    yield break;
                }

                yield return null;
            }

            if (!builtRoom)
            {
                Debug.LogWarning("MRUK did not load a room. Check Quest Space Setup and scene permissions.");
            }
        }

        private void OnSceneLoaded()
        {
            var room = GetCurrentRoom();
            if (room != null)
            {
                BuildFromRoom(room);
            }
        }

        private void BuildFromRoom(MRUKRoom room)
        {
            if (builtRoom)
            {
                return;
            }

            ClearSurfaceCopies();

            foreach (var floor in room.FloorAnchors)
            {
                CreateSurfaceCopy(floor, "Floor", floorColor, false);
            }

            foreach (var wall in room.WallAnchors)
            {
                CreateSurfaceCopy(wall, "Wall", wallColor, true);
            }

            navMeshSurface.BuildNavMesh();
            builtRoom = true;
        }

        private bool CreateSurfaceCopy(MRUKAnchor anchor, string label, Color color, bool blocksNavigation)
        {
            if (!anchor.PlaneRect.HasValue)
            {
                return false;
            }

            anchor.gameObject.layer = surfaceLayer;

            var rect = anchor.PlaneRect.Value;
            var boundary = GetBoundary(anchor, rect);
            var copy = new GameObject($"MRUK_{label}_Copy");
            copy.layer = surfaceLayer;
            copy.transform.SetParent(surfaceRoot, false);
            copy.transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);

            var mesh = BuildPlaneMesh(boundary, rect);
            copy.AddComponent<MeshFilter>().sharedMesh = mesh;
            copy.AddComponent<MeshCollider>().sharedMesh = mesh;
            CreateSurfaceOutline(copy.transform, boundary, color);

            if (blocksNavigation)
            {
                AddWallNavigationBlocker(copy, rect);
            }

            return true;
        }

        private void AddWallNavigationBlocker(GameObject wallCopy, Rect rect)
        {
            var center = new Vector3(rect.center.x, rect.center.y, 0f);
            var size = new Vector3(rect.width, rect.height, wallColliderThickness);

            var collider = wallCopy.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;

            var volume = wallCopy.AddComponent<NavMeshModifierVolume>();
            volume.center = center;
            volume.size = size;
            volume.area = NavMesh.GetAreaFromName("Not Walkable");
        }

        private void ClearSurfaceCopies()
        {
            for (int i = surfaceRoot.childCount - 1; i >= 0; i--)
            {
                var child = surfaceRoot.GetChild(i);
                if (child.name.StartsWith("MRUK_", System.StringComparison.Ordinal))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void CreateSurfaceOutline(Transform parent, Vector2[] points, Color color)
        {
            if (!showSurfaceOutlines || points.Length < 2)
            {
                return;
            }

            var outlineObject = new GameObject("Surface Outline");
            outlineObject.transform.SetParent(parent, false);

            var line = outlineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = points.Length;
            line.widthMultiplier = outlineWidth;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = GetOutlineMaterial();
            line.startColor = color;
            line.endColor = color;

            for (int i = 0; i < points.Length; i++)
            {
                line.SetPosition(i, new Vector3(points[i].x, points[i].y, outlineOffset));
            }
        }

        private Material GetOutlineMaterial()
        {
            if (outlineMaterial != null)
            {
                return outlineMaterial;
            }

            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");

            outlineMaterial = new Material(shader)
            {
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent
            };

            return outlineMaterial;
        }

        private static MRUKRoom GetCurrentRoom()
        {
            if (MRUK.Instance == null)
            {
                return null;
            }

            return MRUK.Instance.GetCurrentRoom();
        }

        private static Mesh BuildPlaneMesh(Vector2[] boundary, Rect fallbackRect)
        {
            var vertices = new Vector3[boundary.Length];
            var uvs = new Vector2[boundary.Length];

            for (int i = 0; i < boundary.Length; i++)
            {
                vertices[i] = new Vector3(boundary[i].x, boundary[i].y, 0f);
                uvs[i] = new Vector2(
                    Mathf.InverseLerp(fallbackRect.xMin, fallbackRect.xMax, boundary[i].x),
                    Mathf.InverseLerp(fallbackRect.yMin, fallbackRect.yMax, boundary[i].y));
            }

            var triangles = new int[(boundary.Length - 2) * 3];
            int index = 0;
            for (int i = 1; i < boundary.Length - 1; i++)
            {
                triangles[index++] = 0;
                triangles[index++] = i;
                triangles[index++] = i + 1;
            }

            var mesh = new Mesh
            {
                name = "MRUK Surface Copy",
                vertices = vertices,
                uv = uvs,
                triangles = triangles
            };

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Vector2[] GetBoundary(MRUKAnchor anchor, Rect rect)
        {
            if (anchor.PlaneBoundary2D != null && anchor.PlaneBoundary2D.Count >= 3)
            {
                return anchor.PlaneBoundary2D.ToArray();
            }

            return new[]
            {
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMin, rect.yMax)
            };
        }
    }
}
