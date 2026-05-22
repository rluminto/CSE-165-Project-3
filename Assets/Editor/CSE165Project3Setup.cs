using System;
using System.Collections.Generic;
using CSE165.Project3;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CSE165.Project3.Editor
{
    public static class CSE165Project3Setup
    {
        private const string CameraRigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        private const string HandPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRHandPrefab.prefab";
        private const string MRUKPrefabPath = "Packages/com.meta.xr.mrutilitykit/Core/Tools/MRUK.prefab";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string MarkerMaterialPath = "Assets/DestinationMarker.mat";
        private const string CharacterRootPath = "Assets/Characters";
        private const string AgentControllerPath = "Assets/Characters/Agent.controller";
        private const float MixamoAgentScale = 0.45f;
        private const float AgentRadius = 0.08f;
        private const float AgentHeight = 0.85f;

        [MenuItem("CSE 165/Project 3/Setup Complete Project")]
        public static void SetupCompleteProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Exit Play Mode before running CSE 165 > Project 3 > Setup Complete Project.");
                return;
            }

            EnsureSceneOpen();
            ConfigureProjectCapabilities();

            int surfaceLayer = EnsureLayer("Surface");
            var cameraRig = EnsureCameraRig();
            var destinationMarker = EnsureDestinationMarker(EnsureMarkerMaterial());

            EnsurePassthrough(cameraRig);
            EnsureMRUK();
            EnsureSurfaceSystem(surfaceLayer);
            EnsureHands(cameraRig, destinationMarker);
            EnsureMixamoAgent(destinationMarker);
            EnsureSceneInBuildSettings();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            Debug.Log("CSE 165 Project 3 setup complete: passthrough, MRUK surfaces, hand pinch destination, and Mixamo agent are wired.");
        }

        private static void ConfigureProjectCapabilities()
        {
            ConfigureOculusProjectConfig();
            EnableOpenXRFeatures();
            ConfigureNavMeshAgentSettings();
            EnsureSceneInBuildSettings();
        }

        private static void EnsureSceneOpen()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        private static GameObject EnsureCameraRig()
        {
            var rig = FindSceneObject("OVRCameraRig");
            if (rig == null)
            {
                DeleteSceneObject("Main Camera");
                rig = (GameObject)PrefabUtility.InstantiatePrefab(LoadRequiredAsset<GameObject>(CameraRigPrefabPath));
                rig.name = "OVRCameraRig";
            }

            ConfigureCameraRig(rig);
            return rig;
        }

        private static void ConfigureCameraRig(GameObject rig)
        {
            var manager = rig.GetComponent<OVRManager>();
            manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            manager.isInsightPassthroughEnabled = true;

            foreach (var camera in rig.GetComponentsInChildren<Camera>(true))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
            }
        }

        private static void EnsurePassthrough(GameObject cameraRig)
        {
            var layer = cameraRig.GetComponent<OVRPassthroughLayer>() ?? cameraRig.AddComponent<OVRPassthroughLayer>();

#pragma warning disable CS0618
            layer.overlayType = OVROverlay.OverlayType.Underlay;
            layer.projectionSurfaceType = OVRPassthroughLayer.ProjectionSurfaceType.Reconstructed;
#pragma warning restore CS0618
            layer.textureOpacity = 1f;
            layer.hidden = false;

            var bootstrap = cameraRig.GetComponent<PassthroughBootstrap>() ?? cameraRig.AddComponent<PassthroughBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("passthroughLayer").objectReferenceValue = layer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureMRUK()
        {
            var existingMRUK = Object.FindAnyObjectByType<Meta.XR.MRUtilityKit.MRUK>();
            GameObject mrukObject;
            if (existingMRUK != null)
            {
                mrukObject = existingMRUK.gameObject;
            }
            else
            {
                mrukObject = (GameObject)PrefabUtility.InstantiatePrefab(LoadRequiredAsset<GameObject>(MRUKPrefabPath));
            }

            mrukObject.name = "MRUK";
            ConfigureMRUK(mrukObject);
        }

        private static void ConfigureMRUK(GameObject mrukObject)
        {
            var mruk = mrukObject.GetComponent<Meta.XR.MRUtilityKit.MRUK>();
            var serialized = new SerializedObject(mruk);
            SetBool(serialized, "EnableWorldLock", true);
            SetEnum(serialized, "SceneSettings.DataSource", (int)Meta.XR.MRUtilityKit.MRUK.SceneDataSource.Device);
            SetBool(serialized, "SceneSettings.LoadSceneOnStartup", true);
            SetBool(serialized, "SceneSettings.EnableHighFidelityScene", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureSurfaceSystem(int surfaceLayer)
        {
            var root = FindSceneObject("Room Surface Copies") ?? new GameObject("Room Surface Copies");
            root.layer = surfaceLayer;

            var navSurface = root.GetComponent<NavMeshSurface>() ?? root.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.Children;
            navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navSurface.layerMask = 1 << surfaceLayer;
            navSurface.defaultArea = NavMesh.GetAreaFromName("Walkable");

            var manager = root.GetComponent<RoomSurfaceManager>() ?? root.AddComponent<RoomSurfaceManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("surfaceRoot").objectReferenceValue = root.transform;
            serialized.FindProperty("navMeshSurface").objectReferenceValue = navSurface;
            serialized.FindProperty("surfaceLayerName").stringValue = "Surface";
            SetBool(serialized, "showSurfaceOutlines", true);
            SetFloat(serialized, "outlineOffset", 0.025f);
            SetFloat(serialized, "outlineWidth", 0.012f);
            SetFloat(serialized, "wallColliderThickness", 0.06f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

        }

        private static Transform EnsureDestinationMarker(Material markerMaterial)
        {
            var markers = FindSceneObjects("Destination Marker");
            GameObject marker = markers.Length > 0 ? markers[0] : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Destination Marker";
            marker.transform.localScale = Vector3.one * 0.12f;
            marker.SetActive(false);

            var renderer = marker.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = markerMaterial;

            for (int i = 1; i < markers.Length; i++)
            {
                Object.DestroyImmediate(markers[i]);
            }

            WireDestinationMarkerReferences(marker.transform);
            return marker.transform;
        }

        private static void EnsureHands(GameObject cameraRig, Transform destinationMarker)
        {
            EnsureHand(FindChild(cameraRig.transform, "LeftHandAnchor"), "Left OVRHand", 1, destinationMarker, false);
            EnsureHand(FindChild(cameraRig.transform, "RightHandAnchor"), "Right OVRHand", 2, destinationMarker, true);
        }

        private static void EnsureHand(Transform anchor, string objectName, int handTypeIndex, Transform destinationMarker, bool addGestureSelector)
        {
            var hand = anchor.GetComponentInChildren<OVRHand>(true);
            var handObject = hand != null
                ? hand.gameObject
                : (GameObject)PrefabUtility.InstantiatePrefab(LoadRequiredAsset<GameObject>(HandPrefabPath), anchor);

            handObject.name = objectName;
            handObject.transform.localPosition = Vector3.zero;
            handObject.transform.localRotation = Quaternion.identity;

            var serializedHand = new SerializedObject(handObject.GetComponent<OVRHand>());
            serializedHand.FindProperty("HandType").enumValueIndex = handTypeIndex;
            serializedHand.ApplyModifiedPropertiesWithoutUndo();
            handObject.GetComponent<OVRHand>().OnValidate();

            if (!addGestureSelector)
            {
                return;
            }

            var selector = handObject.GetComponent<HandGestureDestinationSelector>() ?? handObject.AddComponent<HandGestureDestinationSelector>();
            var serializedSelector = new SerializedObject(selector);
            serializedSelector.FindProperty("hand").objectReferenceValue = handObject.GetComponent<OVRHand>();
            serializedSelector.FindProperty("skeleton").objectReferenceValue = handObject.GetComponent<OVRSkeleton>();
            serializedSelector.FindProperty("destinationMarker").objectReferenceValue = destinationMarker;
            SetBool(serializedSelector, "showPointerRay", true);
            SetFloat(serializedSelector, "pointerWidth", 0.012f);
            SetFloat(serializedSelector, "pointerMarkerScale", 0.08f);
            SetColor(serializedSelector, "validPointerColor", new Color(0.1f, 1f, 0.35f, 0.95f));
            SetColor(serializedSelector, "invalidPointerColor", new Color(1f, 0.2f, 0.05f, 0.75f));
            serializedSelector.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureMixamoAgent(Transform destinationMarker)
        {
            var characterPrefab = FindCharacterModel();
            var idleClip = FindAnimationClip("Idle");
            var walkClip = FindAnimationClip("Walk") ?? FindAnimationClip("Catwalk");

            if (characterPrefab == null || idleClip == null || walkClip == null)
            {
                Debug.LogError("Mixamo setup needs one character FBX under Assets/Characters and Idle plus Walk/Catwalk clips under Assets/Characters/animations.");
                return;
            }

            var agentObject = FindSceneObject("MixamoAgent") ?? (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
            agentObject.name = "MixamoAgent";
            ConfigureAgentTransform(agentObject);
            ApplyExtractedMixamoMaterial(agentObject);

            var animator = agentObject.GetComponentInChildren<Animator>() ?? agentObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = EnsureAgentAnimatorController(idleClip, walkClip);
            animator.applyRootMotion = false;

            var navAgent = agentObject.GetComponent<NavMeshAgent>() ?? agentObject.AddComponent<NavMeshAgent>();
            navAgent.speed = 0.8f;
            navAgent.angularSpeed = 120f;
            navAgent.acceleration = 8f;
            navAgent.radius = AgentRadius;
            navAgent.height = AgentHeight;
            navAgent.baseOffset = 0f;

            var controller = agentObject.GetComponent<AgentNavigationController>() ?? agentObject.AddComponent<AgentNavigationController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("agent").objectReferenceValue = navAgent;
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("destinationMarker").objectReferenceValue = destinationMarker;
            serialized.FindProperty("walkingParameter").stringValue = "Walking";
            serialized.FindProperty("initialDistanceFromUser").floatValue = 0.9f;
            serialized.FindProperty("initialSideOffset").floatValue = 0.25f;
            serialized.FindProperty("initialPlacementSampleRadius").floatValue = 1.75f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAgentTransform(GameObject agentObject)
        {
            agentObject.transform.localScale = Vector3.one * MixamoAgentScale;
            agentObject.transform.position = new Vector3(0.25f, 0f, 0.9f);
            agentObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private static void ApplyExtractedMixamoMaterial(GameObject agentObject)
        {
            var material = FindExtractedMixamoBodyMaterial();
            if (material == null)
            {
                return;
            }

            foreach (var renderer in agentObject.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static void WireDestinationMarkerReferences(Transform destinationMarker)
        {
            foreach (var selector in Resources.FindObjectsOfTypeAll<HandGestureDestinationSelector>())
            {
                if (!selector.gameObject.scene.IsValid())
                {
                    continue;
                }

                var serialized = new SerializedObject(selector);
                serialized.FindProperty("destinationMarker").objectReferenceValue = destinationMarker;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (var controller in Resources.FindObjectsOfTypeAll<AgentNavigationController>())
            {
                if (!controller.gameObject.scene.IsValid())
                {
                    continue;
                }

                var serialized = new SerializedObject(controller);
                serialized.FindProperty("destinationMarker").objectReferenceValue = destinationMarker;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static GameObject FindSceneObject(string objectName)
        {
            var matches = FindSceneObjects(objectName);
            return matches.Length > 0 ? matches[0] : null;
        }

        private static GameObject[] FindSceneObjects(string objectName)
        {
            var matches = new List<GameObject>();
            foreach (var gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject.name == objectName && gameObject.scene.IsValid())
                {
                    matches.Add(gameObject);
                }
            }

            return matches.ToArray();
        }

        private static void DeleteSceneObject(string objectName)
        {
            foreach (var gameObject in FindSceneObjects(objectName))
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                var result = FindChild(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static GameObject FindCharacterModel()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharacterRootPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/animations/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        private static AnimationClip FindAnimationClip(string nameContains)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharacterRootPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("/animations/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip &&
                        !clip.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                        clip.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

        private static Material FindExtractedMixamoBodyMaterial()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { CharacterRootPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && HasTexture(material))
                {
                    return material;
                }
            }

            return null;
        }

        private static bool HasTexture(Material material)
        {
            return (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null) ||
                   (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null) ||
                   (material.HasProperty("_BaseColorMap") && material.GetTexture("_BaseColorMap") != null);
        }

        private static AnimatorController EnsureAgentAnimatorController(AnimationClip idleClip, AnimationClip walkClip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AgentControllerPath) ??
                             AnimatorController.CreateAnimatorControllerAtPath(AgentControllerPath);

            if (!HasWalkingParameter(controller))
            {
                controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
            }

            var stateMachine = controller.layers[0].stateMachine;
            ClearStateMachine(stateMachine);

            var idleState = stateMachine.AddState("Idle", new Vector3(250f, 100f, 0f));
            idleState.motion = idleClip;
            stateMachine.defaultState = idleState;

            var walkState = stateMachine.AddState("Walk", new Vector3(520f, 100f, 0f));
            walkState.motion = walkClip;

            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.1f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "Walking");

            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.1f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Walking");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static bool HasWalkingParameter(AnimatorController controller)
        {
            foreach (var parameter in controller.parameters)
            {
                if (parameter.name == "Walking" && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ClearStateMachine(AnimatorStateMachine stateMachine)
        {
            foreach (var transition in stateMachine.anyStateTransitions)
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }

            foreach (var transition in stateMachine.entryTransitions)
            {
                stateMachine.RemoveEntryTransition(transition);
            }

            foreach (var childState in stateMachine.states)
            {
                stateMachine.RemoveState(childState.state);
            }

            foreach (var childMachine in stateMachine.stateMachines)
            {
                stateMachine.RemoveStateMachine(childMachine.stateMachine);
            }
        }

        private static Material EnsureMarkerMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MarkerMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, MarkerMaterialPath);
            }

            ConfigureTransparentMaterial(material, new Color(0.2f, 1f, 0.25f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTransparentMaterial(Material material, Color color)
        {
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
        }

        private static void EnsureSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == ScenePath)
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes;
                    return;
                }
            }

            ArrayUtility.Add(ref scenes, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes;
        }

        private static void ConfigureOculusProjectConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Oculus/OculusProjectConfig.asset");
            if (config == null)
            {
                Debug.LogWarning("OculusProjectConfig.asset was not found.");
                return;
            }

            var serialized = new SerializedObject(config);
            SetEnum(serialized, "handTrackingSupport", 1);
            SetEnum(serialized, "anchorSupport", 1);
            SetEnum(serialized, "sceneSupport", 2);
            SetEnum(serialized, "_insightPassthroughSupport", 2);
            SetBool(serialized, "insightPassthroughEnabled", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static void EnableOpenXRFeatures()
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath("Assets/XR/Settings/OpenXRPackageSettings.asset"))
            {
                switch (obj.name)
                {
                    case "MetaXRFeature Android":
                    case "MetaQuestFeature Android":
                    case "HandTracking Android":
                    case "MetaHandTrackingAim Android":
                    case "HandInteractionProfile Android":
                    case "PalmPoseInteraction Android":
                    case "HandCommonPosesInteraction Android":
                        var serialized = new SerializedObject(obj);
                        var enabled = serialized.FindProperty("m_enabled");
                        if (enabled != null)
                        {
                            enabled.boolValue = true;
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(obj);
                        }
                        break;
                }
            }
        }

        private static void ConfigureNavMeshAgentSettings()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            var serialized = new SerializedObject(assets[0]);
            var settings = serialized.FindProperty("m_Settings");
            if (settings == null)
            {
                return;
            }

            for (int i = 0; i < settings.arraySize; i++)
            {
                var agentSettings = settings.GetArrayElementAtIndex(i);
                if (agentSettings.FindPropertyRelative("agentTypeID").intValue != 0)
                {
                    continue;
                }

                agentSettings.FindPropertyRelative("agentRadius").floatValue = AgentRadius;
                agentSettings.FindPropertyRelative("agentHeight").floatValue = AgentHeight;
                agentSettings.FindPropertyRelative("agentClimb").floatValue = 0.2f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(assets[0]);
                return;
            }
        }

        private static int EnsureLayer(string layerName)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    return i;
                }
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }

            throw new InvalidOperationException($"No empty user layer slot was available for '{layerName}'.");
        }

        private static T LoadRequiredAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"Required asset was not found: {path}");
            }

            return asset;
        }

        private static void SetEnum(SerializedObject serialized, string propertyName, int value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.enumValueIndex = value;
            }
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetColor(SerializedObject serialized, string propertyName, Color value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.colorValue = value;
            }
        }
    }
}
