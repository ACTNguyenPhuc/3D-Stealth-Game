using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using BindingMode = Unity.Cinemachine.TargetTracking.BindingMode;

namespace StealthGame.EditorTools
{
    public static partial class GameBuilder
    {
        // ================================================================== Bố cục level (toạ độ thế giới, đã kiểm tra bằng NavMesh)

        struct Spot
        {
            public string id, name;
            public Vector3 pos;
            public float yaw;
            public Spot(string id, string name, float x, float z, float yaw = 0f) { this.id = id; this.name = name; pos = new Vector3(x, 0f, z); this.yaw = yaw; }
        }

        static readonly Spot[] k_Checkpoints =
        {
            new("cp_start", "Phòng ngủ", -11.1f, -5.2f, 90f),
            new("cp_hall", "Sảnh lớn", -10.8f, 2.6f, 90f),
            new("cp_center", "Hành lang giữa", -1.7f, -4.3f, 0f),
            new("cp_east", "Hành lang phía Đông", 10.5f, 2.3f, 90f),
        };

        static readonly (string id, string name, Vector3 pos, string seal)[] k_Relics =
        {
            ("relic_kitchen", "Mảnh hồn Bình Minh", new Vector3(-4.6f, 0f, -4.6f), null),
            ("relic_study", "Mảnh hồn Hoàng Hôn", new Vector3(-4.2f, 0f, 10.3f), "red"),
            ("relic_dining", "Mảnh hồn Nửa Đêm", new Vector3(7.4f, 0f, 12.6f), "blue"),
        };

        static readonly Vector2[] k_Shards =
        {
            new(-11.3f, -7.0f), new(-7.9f, -4.6f), new(-9.0f, -2.5f), new(-11.4f, 0.5f), new(-8.0f, 3.0f),
            new(-13.0f, -1.8f), new(-15.2f, -2.2f), new(-18.6f, -0.2f), new(-16.8f, -2.6f), new(-6.5f, 1.0f),
            new(-3.9f, 3.2f), new(-11.0f, 7.0f), new(-6.8f, 6.8f), new(-4.0f, 5.0f), new(-6.2f, -2.3f),
            new(-1.7f, 2.0f), new(-1.7f, -7.5f), new(0.8f, -9.2f), new(-1.8f, -9.3f), new(1.0f, 4.5f),
            new(1.0f, -2.5f), new(-0.8f, 9.4f), new(3.5f, 4.0f), new(6.0f, -1.8f), new(7.6f, -4.8f),
            new(3.0f, -7.5f), new(12.5f, 0.3f), new(14.8f, 2.6f), new(3.0f, 7.0f), new(5.6f, 13.0f),
        };

        static readonly Vector2[] k_Potions = { new(-10.6f, 6.0f), new(-0.4f, -8.6f), new(4.2f, -6.8f), new(13.5f, 2.6f) };
        static readonly Vector2[] k_Energy = { new(-5.0f, 2.0f), new(-18.5f, -2.6f), new(0.9f, -5.8f), new(-2.5f, 10.8f), new(7.8f, -3.6f), new(8.3f, 7.0f) };

        static readonly (string id, bool wraith, Vector2[] route)[] k_Patrols =
        {
            ("ghost_kitchen", false, new[] { new Vector2(-5.6f, -4.8f), new Vector2(-3.8f, -2.6f), new Vector2(-5.6f, 1.8f), new Vector2(-4.0f, 3.0f) }),
            ("ghost_corridor_w", false, new[] { new Vector2(-1.7f, -6.8f), new Vector2(-1.7f, 6.8f) }),
            ("ghost_corridor_e", false, new[] { new Vector2(1.0f, -6.0f), new Vector2(1.0f, 7.6f), new Vector2(-2.5f, 9.6f), new Vector2(1.0f, 7.6f) }),
            ("ghost_east", false, new[] { new Vector2(5.8f, 1.6f), new Vector2(14.5f, 1.6f) }),
            ("ghost_southeast", false, new[] { new Vector2(6.8f, -1.8f), new Vector2(7.6f, -4.6f), new Vector2(3.2f, -3.0f) }),
            ("wraith_dining", true, new[] { new Vector2(3.0f, 6.0f), new Vector2(3.0f, 12.4f), new Vector2(7.8f, 12.4f), new Vector2(7.8f, 6.2f) }),
        };

        // ================================================================== Scene gameplay

        static void BuildGameScene(BuiltAssets a)
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            a = LoadBuilt();

            // Ghi nhớ vị trí tượng Gargoyle cũ để tái sử dụng
            var oldGargoyles = new List<(Vector3 pos, Quaternion rot)>();
            var enemiesOld = FindRoot(scene, "Enemies");
            if (enemiesOld != null)
            {
                foreach (Transform t in enemiesOld.transform)
                    if (t.name.StartsWith("Gargoyle")) oldGargoyles.Add((t.position, t.rotation));
                var shower = enemiesOld.transform.Find("Ghost Shower");
                if (shower != null)
                {
                    shower.SetParent(null, true);
                    StripLegacy(shower.gameObject);
                    shower.name = "Decor_GhostShower";
                }
            }

            // Xóa hệ thống cũ / do tool tạo trước đó
            foreach (var n in new[] { "Enemies", "Waypoints", "GameEnding", "FaderCanvas", "Camera System", "Dialogue System", "NPCs",
                                      "Game Systems", "Game UI", "Level Content", "Patrol Routes" })
                DestroyRoots(scene, n);

            var audioRoot = FindRoot(scene, "Audio");
            if (audioRoot != null)
            {
                foreach (var n in new[] { "Escape", "Caught" })
                {
                    var t = audioRoot.transform.Find(n);
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }
                foreach (var src in audioRoot.GetComponentsInChildren<AudioSource>())
                    if (src.GetComponent<AudioVolumeBinder>() == null) src.gameObject.AddComponent<AudioVolumeBinder>().category = SoundCategory.Ambient;
            }

            DarkenOutside(FindRoot(scene, "Level A"));
            var player = SetupPlayer(scene, a, out var chest, out var head);
            BakeNavMesh(scene);
            SetupCamera(scene, player, chest, head);
            var content = SetupLevelContent(scene, a);
            SetupEnemies(scene, a, oldGargoyles);
            var systems = SetupSystems(scene, a, player, head);
            SetupNpcs(scene, a);
            SetupUI(scene, a, systems);
            SetupDoorsAndKeys(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GameBuilder] Scene gameplay đã dựng xong.");
        }

        /// <summary>Thay mặt đất sương mù vàng bên ngoài dinh thự bằng nền đêm tối (chỉ override trong scene).</summary>
        static void DarkenOutside(GameObject level)
        {
            if (level == null) return;
            var dark = Mat("OutsideNight", "Universal Render Pipeline/Unlit", new Color(0.025f, 0.03f, 0.06f));
            foreach (var r in level.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name.StartsWith("FogPlane")) { mats[i] = dark; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static GameObject FindRoot(Scene scene, string name) => scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);

        static void DestroyRoots(Scene scene, string name)
        {
            foreach (var g in scene.GetRootGameObjects())
                if (g.name == name) Object.DestroyImmediate(g);
        }

        static Vector3 Snap(Vector3 p, float maxDistance = 2f)
        {
            if (NavMesh.SamplePosition(p + Vector3.up * 0.5f, out var hit, maxDistance, NavMesh.AllAreas))
                return hit.position;
            Debug.LogWarning($"[GameBuilder] Điểm {p} không nằm trên NavMesh – giữ nguyên.");
            return p;
        }

        // ------------------------------------------------------------------ Player

        static PlayerStats SetupPlayer(Scene scene, BuiltAssets a, out Transform chest, out Transform head)
        {
            var movement = Object.FindAnyObjectByType<PlayerMovement>();
            var go = movement.gameObject;
            go.name = "Player";
            go.tag = "Player";

            var rb = go.GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.mass = 1f;
            var capsule = go.GetComponent<CapsuleCollider>();
            string pmPath = GenData + "/PlayerNoFriction.physicsMaterial";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(pmPath);
            if (pm == null)
            {
                pm = new PhysicsMaterial("PlayerNoFriction") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
                AssetDatabase.CreateAsset(pm, pmPath);
            }
            capsule.sharedMaterial = pm;

            var stats = go.GetComponent<PlayerStats>() ? go.GetComponent<PlayerStats>() : go.AddComponent<PlayerStats>();
            if (go.GetComponent<PlayerInventory>() == null) go.AddComponent<PlayerInventory>();
            var combat = go.GetComponent<PlayerCombat>() ? go.GetComponent<PlayerCombat>() : go.AddComponent<PlayerCombat>();
            if (go.GetComponent<PlayerInteractor>() == null) go.AddComponent<PlayerInteractor>();
            var anim = go.GetComponent<PlayerAnimation>() ? go.GetComponent<PlayerAnimation>() : go.AddComponent<PlayerAnimation>();

            var animator = go.GetComponent<Animator>();
            animator.runtimeAnimatorController = a.playerController;
            animator.applyRootMotion = false;
            anim.animator = animator;
            foreach (Transform child in go.transform)
            {
                if (child.GetComponentInChildren<SkinnedMeshRenderer>() != null) { anim.model = child; break; }
            }

            // Xóa các anchor / đèn cũ rồi tạo lại
            foreach (var n in new[] { "CamTarget_Chest", "CamTarget_Head", "Lantern", "BurstLight" })
            {
                var t = go.transform.Find(n);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            chest = Child(go.transform, "CamTarget_Chest", new Vector3(0f, 0.75f, 0f)).transform;
            head = Child(go.transform, "CamTarget_Head", new Vector3(0f, 1.1f, 0.08f)).transform;

            var lantern = AddLight(go.transform, "Lantern", new Vector3(0.2f, 0.95f, 0.25f), LightType.Spot, new Color(1f, 0.86f, 0.6f), 10f, 6f);
            lantern.spotAngle = 65f;
            lantern.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            lantern.shadows = LightShadows.Soft;
            combat.lanternLight = lantern;
            combat.burstLight = AddLight(go.transform, "BurstLight", new Vector3(0f, 0.9f, 0.6f), LightType.Point, new Color(1f, 0.95f, 0.75f), 8f, 0f);

            var footsteps = go.GetComponent<AudioSource>();
            if (footsteps != null) { footsteps.playOnAwake = false; footsteps.loop = true; footsteps.volume = 0.6f; }

            // Vị trí xuất phát mặc định
            go.transform.position = new Vector3(-9.39f, 0f, -5.73f);
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            return stats;
        }

        // ------------------------------------------------------------------ NavMesh

        static void BakeNavMesh(Scene scene)
        {
            var level = FindRoot(scene, "Level A");
            var surface = GetOrAdd<NavMeshSurface>(level);
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            string path = Root + "/Tutorial_Demo/Demo_Scenes/DemoScene Data/NavMesh-LevelA.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        }

        // ------------------------------------------------------------------ Camera

        static CinemachineCamera NewCam(string name, Transform parent, Transform target, float fov)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<CinemachineCamera>();
            cam.Target.TrackingTarget = target;
            cam.Lens = LensSettings.Default;
            cam.Lens.FieldOfView = fov;
            cam.Lens.NearClipPlane = 0.1f;
            cam.Lens.FarClipPlane = 300f;
            cam.Priority = 0;
            go.AddComponent<CinemachineImpulseListener>();
            return cam;
        }

        static void SetupCamera(Scene scene, PlayerStats player, Transform chest, Transform head)
        {
            var root = new GameObject("Camera System");
            var mainCam = Camera.main;
            var brain = GetOrAdd<CinemachineBrain>(mainCam.gameObject);
            brain.IgnoreTimeScale = true;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.8f);
            brain.CustomBlends = BuildBlends();

            // 1. Góc nhìn thứ ba (mặc định)
            var orbitCam = NewCam("CM ThirdPerson Orbit", root.transform, chest, 58f);
            var orbit = orbitCam.gameObject.AddComponent<CinemachineOrbitalFollow>();
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = 3.4f;
            orbit.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            orbit.TrackerSettings.PositionDamping = new Vector3(0.1f, 0.1f, 0.1f);
            orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbit.HorizontalAxis.Wrap = true;
            orbit.VerticalAxis.Range = new Vector2(-5f, 70f);
            orbit.VerticalAxis.Center = 25f;
            orbit.VerticalAxis.Value = 25f;
            orbit.RadialAxis.Range = new Vector2(0.45f, 2.0f);
            orbit.RadialAxis.Center = 1f;
            orbit.RadialAxis.Value = 1f;
            var orbitAim = orbitCam.gameObject.AddComponent<CinemachineRotationComposer>();
            orbitAim.Damping = new Vector2(0.05f, 0.05f);
            orbitAim.Composition.ScreenPosition = new Vector2(0f, 0.08f);
            var deocc = orbitCam.gameObject.AddComponent<CinemachineDeoccluder>();
            deocc.CollideAgainst = 1;
            deocc.IgnoreTag = "Player";
            deocc.MinimumDistanceFromTarget = 0.3f;
            var avoid = deocc.AvoidObstacles;
            avoid.Enabled = true;
            avoid.CameraRadius = 0.2f;
            avoid.Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward;
            avoid.MaximumEffort = 4;
            avoid.Damping = 0.25f;
            avoid.DampingWhenOccluded = 0f;
            deocc.AvoidObstacles = avoid;
            var orbitInput = orbitCam.gameObject.AddComponent<CameraLookInput>();
            orbitInput.mouseActivation = CameraLookInput.MouseActivation.Always;
            orbitInput.mouseSensitivity = 0.12f;
            orbitInput.enabled = false;

            // 2. Top-down (Follow Camera có sẵn)
            var followCam = Object.FindObjectsByType<CinemachineCamera>().FirstOrDefault(c => c.name == "Follow Camera");
            if (followCam == null)
            {
                followCam = NewCam("Follow Camera", null, player.transform, 60f);
                var f = followCam.gameObject.AddComponent<CinemachineFollow>();
                f.FollowOffset = new Vector3(0f, 6f, -3.2f);
                f.TrackerSettings.BindingMode = BindingMode.WorldSpace;
                followCam.gameObject.AddComponent<CinemachineRotationComposer>();
            }
            followCam.Target.TrackingTarget = player.transform;
            var follow = followCam.GetComponent<CinemachineFollow>();
            if (follow != null) follow.FollowOffset = new Vector3(0f, 6.2f, -3.4f);
            if (followCam.GetComponent<CinemachineImpulseListener>() == null) followCam.gameObject.AddComponent<CinemachineImpulseListener>();

            // 3. Góc nhìn thứ nhất
            var fpsCam = NewCam("CM FirstPerson", root.transform, head, 72f);
            fpsCam.Lens.NearClipPlane = 0.05f;
            fpsCam.gameObject.AddComponent<CinemachineHardLockToTarget>();
            var panTilt = fpsCam.gameObject.AddComponent<CinemachinePanTilt>();
            panTilt.ReferenceFrame = CinemachinePanTilt.ReferenceFrames.World;
            panTilt.PanAxis.Range = new Vector2(-180f, 180f);
            panTilt.PanAxis.Wrap = true;
            panTilt.TiltAxis.Range = new Vector2(-70f, 70f);
            var fpsInput = fpsCam.gameObject.AddComponent<CameraLookInput>();
            fpsInput.mouseActivation = CameraLookInput.MouseActivation.Always;
            fpsInput.mouseSensitivity = 0.1f;
            fpsInput.enabled = false;

            // 4. Camera an ninh (ClearShot)
            var clearShot = new GameObject("CM Security Cameras").AddComponent<CinemachineClearShot>();
            clearShot.transform.SetParent(root.transform, false);
            clearShot.Priority = 0;
            clearShot.ActivateAfter = 0.15f;
            clearShot.MinDuration = 1f;
            clearShot.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            var cctvSpots = new List<Vector3> { new(-9.4f, 2.7f, -5.7f) };
            foreach (var p in k_Patrols) foreach (var r in p.route) cctvSpots.Add(new Vector3(r.x, 2.7f, r.y));
            var positions = new List<Vector3>();
            foreach (var p in cctvSpots) if (positions.All(q => Vector3.Distance(q, p) > 3f)) positions.Add(p);
            for (int i = 0; i < positions.Count; i++)
            {
                var cam = NewCam($"CAM-{i + 1:00}", clearShot.transform, chest, 62f);
                cam.transform.position = positions[i];
                var aim = cam.gameObject.AddComponent<CinemachineRotationComposer>();
                aim.Damping = new Vector2(0.5f, 0.5f);
                var q = cam.gameObject.AddComponent<CinemachineShotQualityEvaluator>();
                q.OcclusionLayers = 1;
                q.IgnoreTag = "Player";
                var d = q.DistanceEvaluation;
                d.Enabled = true; d.NearLimit = 1f; d.FarLimit = 12f; d.OptimalDistance = 4f; d.MaxQualityBoost = 0.5f;
                q.DistanceEvaluation = d;
            }

            // Camera hội thoại
            var group = new GameObject("Dialogue Target Group").AddComponent<CinemachineTargetGroup>();
            group.transform.SetParent(root.transform, false);
            var dialogueCam = NewCam("CM Dialogue", root.transform, null, 40f);
            dialogueCam.gameObject.AddComponent<CinemachineRotationComposer>().Damping = new Vector2(0.4f, 0.4f);
            dialogueCam.Target.CustomLookAtTarget = true;
            dialogueCam.Target.LookAtTarget = group.transform;
            dialogueCam.Target.TrackingTarget = group.transform;

            var manager = root.AddComponent<CameraModeManager>();
            manager.player = player.transform;
            manager.dialogueCamera = dialogueCam;
            manager.dialogueTargetGroup = group;
            manager.startModeIndex = 0;
            manager.modes = new List<CameraModeDefinition>
            {
                new() { displayName = "Góc nhìn thứ ba", description = "Chuột xoay camera, cuộn để zoom, tự né tường.",
                        virtualCamera = orbitCam, moveBasis = MoveBasis.CameraYaw, lockCursor = true, lookInput = orbitInput },
                new() { displayName = "Top-Down", description = "Góc nhìn chiến thuật từ trên cao.",
                        virtualCamera = followCam, moveBasis = MoveBasis.World },
                new() { displayName = "Góc nhìn thứ nhất", description = "Nhìn qua mắt John Lemon.",
                        virtualCamera = fpsCam, moveBasis = MoveBasis.CameraYaw, faceCameraDirection = true, lockCursor = true, hidePlayerModel = true, lookInput = fpsInput },
                new() { displayName = "Camera an ninh", description = "Các camera cố định tự chọn góc thấy rõ nhất.",
                        virtualCamera = clearShot, moveBasis = MoveBasis.CameraYawLatched, showSecurityOverlay = true },
            };
            foreach (var m in manager.modes) m.virtualCamera.Priority = 0;
            orbitCam.Priority = manager.activePriority;
        }

        static CinemachineBlenderSettings BuildBlends()
        {
            var settings = LoadOrCreate<CinemachineBlenderSettings>(GenData + "/CameraBlends.asset");
            const string any = "**ANY CAMERA**";
            CinemachineBlenderSettings.CustomBlend B(string from, string to, CinemachineBlendDefinition.Styles s, float t)
                => new() { From = from, To = to, Blend = new CinemachineBlendDefinition(s, t) };
            settings.CustomBlends = new[]
            {
                B(any, "CM Security Cameras", CinemachineBlendDefinition.Styles.Cut, 0f),
                B(any, "CM FirstPerson", CinemachineBlendDefinition.Styles.EaseInOut, 0.45f),
                B("CM FirstPerson", any, CinemachineBlendDefinition.Styles.EaseInOut, 0.45f),
                B(any, "CM Dialogue", CinemachineBlendDefinition.Styles.EaseInOut, 0.7f),
                B("CM Dialogue", any, CinemachineBlendDefinition.Styles.EaseInOut, 0.6f),
            };
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // ------------------------------------------------------------------ Vật phẩm, checkpoint, phong ấn, cổng

        static GameObject SetupLevelContent(Scene scene, BuiltAssets a)
        {
            var root = new GameObject("Level Content");

            var cps = Child(root.transform, "Checkpoints", Vector3.zero).transform;
            foreach (var s in k_Checkpoints)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(a.checkpointPrefab, cps);
                go.name = "Checkpoint_" + s.id;
                go.transform.position = Snap(s.pos);
                go.transform.rotation = Quaternion.Euler(0f, s.yaw, 0f);
                var c = go.GetComponent<Checkpoint>();
                c.checkpointId = s.id;
                c.displayName = s.name;
            }

            var relics = Child(root.transform, "Relics", Vector3.zero).transform;
            foreach (var r in k_Relics)
            {
                var anchor = Child(relics, r.id, Snap(r.pos)).transform;
                var pedestal = Primitive(PrimitiveType.Cylinder, "Pedestal", anchor, new Vector3(0f, 0.4f, 0f), new Vector3(0.6f, 0.4f, 0.6f), a.stone, keepCollider: true);
                pedestal.AddComponent<NavMeshObstacle>().carving = true;
                var relic = (GameObject)PrefabUtility.InstantiatePrefab(a.relicPrefab, anchor);
                relic.transform.localPosition = Vector3.zero;
                var p = relic.GetComponent<Pickup>();
                p.pickupId = r.id;
                p.displayName = r.name;

                if (r.seal != null)
                {
                    var seal = (GameObject)PrefabUtility.InstantiatePrefab(a.sealPrefab, anchor);
                    seal.transform.localPosition = Vector3.zero;
                    var ks = seal.GetComponent<KeySeal>();
                    bool red = r.seal == "red";
                    ks.sealId = "seal_" + r.seal;
                    ks.requiredKey = red ? "keyRed" : "keyBlue";
                    ks.sealName = red ? "Phong ấn Đỏ" : "Phong ấn Xanh";
                    ks.keyDisplayName = red ? "Chìa khóa đỏ" : "Chìa khóa xanh";
                    ks.barrier.GetComponent<Renderer>().sharedMaterial = red ? a.sealRed : a.sealBlue;
                }
            }

            void Scatter(string group, Vector2[] spots, GameObject prefab, string prefix)
            {
                var parent = Child(root.transform, group, Vector3.zero).transform;
                for (int i = 0; i < spots.Length; i++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    go.transform.position = Snap(new Vector3(spots[i].x, 0f, spots[i].y));
                    go.name = $"{prefix}_{i + 1:00}";
                    go.GetComponent<Pickup>().pickupId = $"{prefix}_{i + 1:00}";
                }
            }
            Scatter("Shards", k_Shards, a.shardPrefab, "shard");
            Scatter("Potions", k_Potions, a.potionPrefab, "potion");
            Scatter("Energy", k_Energy, a.energyPrefab, "energy");

            var gate = (GameObject)PrefabUtility.InstantiatePrefab(a.exitGatePrefab, root.transform);
            gate.transform.position = new Vector3(15.95f, 0f, 1.3f);
            gate.transform.rotation = Quaternion.identity;
            return root;
        }

        // ------------------------------------------------------------------ Kẻ địch

        static void SetupEnemies(Scene scene, BuiltAssets a, List<(Vector3 pos, Quaternion rot)> gargoyles)
        {
            var root = new GameObject("Enemies");
            var routes = new GameObject("Patrol Routes");

            foreach (var p in k_Patrols)
            {
                var routeRoot = Child(routes.transform, "Route_" + p.id, Vector3.zero).transform;
                var points = new List<Transform>();
                for (int i = 0; i < p.route.Length; i++)
                    points.Add(Child(routeRoot, "WP" + i, Snap(new Vector3(p.route[i].x, 0f, p.route[i].y))).transform);

                var go = (GameObject)PrefabUtility.InstantiatePrefab(p.wraith ? a.wraithPrefab : a.ghostPrefab, root.transform);
                go.name = p.id;
                go.transform.position = points[0].position;
                var ai = go.GetComponent<EnemyAI>();
                ai.enemyId = p.id;
                ai.waypoints = points.ToArray();
                if (ai.config != null) ai.config.dropPrefab = a.shardPrefab;
            }
            EditorUtility.SetDirty(a.ghostConfig);
            EditorUtility.SetDirty(a.wraithConfig);

            if (gargoyles.Count == 0)
            {
                gargoyles.Add((new Vector3(-14.2f, 0f, 0.8f), Quaternion.Euler(0f, 160f, 0f)));
                gargoyles.Add((new Vector3(0.8f, 0f, 10.57f), Quaternion.Euler(0f, 200f, 0f)));
                gargoyles.Add((new Vector3(-2.21f, 0f, -8.33f), Quaternion.Euler(0f, 30f, 0f)));
            }
            for (int i = 0; i < gargoyles.Count; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(a.gargoylePrefab, root.transform);
                go.name = "Gargoyle_" + (i + 1);
                go.transform.SetPositionAndRotation(gargoyles[i].pos, gargoyles[i].rot);
            }
        }

        // ------------------------------------------------------------------ Hệ thống

        struct Systems
        {
            public GameSession session;
            public DialogueManager dialogue;
        }

        static Systems SetupSystems(Scene scene, BuiltAssets a, PlayerStats player, Transform head)
        {
            var root = new GameObject("Game Systems");
            var session = root.AddComponent<GameSession>();
            session.player = player;
            var spawn = Child(root.transform, "DefaultSpawn", new Vector3(-9.39f, 0f, -5.73f));
            spawn.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            session.defaultSpawn = spawn.transform;

            root.AddComponent<QuestManager>().quest = a.quest;

            var dm = root.AddComponent<DialogueManager>();
            dm.player = player.GetComponent<PlayerMovement>();
            dm.playerFocus = head;
            dm.keyDisplayNames = new List<DialogueManager.KeyDisplayName>
            {
                new() { keyName = "keyBlue", displayName = "Chìa khóa xanh" },
                new() { keyName = "keyRed", displayName = "Chìa khóa đỏ" },
            };
            return new Systems { session = session, dialogue = dm };
        }

        // ------------------------------------------------------------------ NPC

        static void SetupNpcs(Scene scene, BuiltAssets a)
        {
            var root = new GameObject("NPCs").transform;
            MakeNpc(root, a, "NPC_Ivan", "Ivan", Root + "/Art/Models/Characters/Players/Ivan_Model.fbx", Root + "/Art/Materials/Characters/Ivan.mat",
                a.npcIdleController, a.ivanTree, new Vector3(-8.26f, 0f, -6.86f), new Vector3(-9.39f, 0f, -5.73f), 1.2f);
            MakeNpc(root, a, "NPC_Whisper", "Whisper (hồn ma)", Root + "/Art/Models/Characters/Enemies/RegularGhost_Model.fbx",
                Root + "/Art/Materials/Characters/GhostDefault.mat",
                Load<RuntimeAnimatorController>(Root + "/Tutorial_Demo/Demo_Animators/Demo_Ghost.controller"), a.whisperTree,
                new Vector3(-9.39f, 0f, 0.27f), new Vector3(-9.39f, 0f, -3f), 1.4f);
        }

        static void MakeNpc(Transform parent, BuiltAssets a, string objectName, string displayName, string modelPath, string matPath,
            RuntimeAnimatorController controller, DialogueTree tree, Vector3 pos, Vector3 lookAt, float height)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(parent, false);
            root.transform.position = Snap(pos);
            var look = lookAt - pos;
            look.y = 0f;
            root.transform.rotation = Quaternion.LookRotation(look);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(modelPath), root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var mat = Load<Material>(matPath);
            if (mat != null)
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                    r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            var animator = GetOrAdd<Animator>(model);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
            capsule.height = height;
            capsule.radius = 0.3f;
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = 0.3f;
            obstacle.height = height;
            obstacle.center = capsule.center;
            obstacle.carving = true;

            var focus = Child(root.transform, "FocusPoint", new Vector3(0f, height * 0.85f, 0f));
            var indicator = Primitive(PrimitiveType.Cube, "TalkIndicator", root.transform, new Vector3(0f, height + 0.35f, 0f), Vector3.one * 0.14f, a.indicator);
            indicator.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            indicator.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var npc = root.AddComponent<NPCDialogue>();
            npc.displayName = displayName;
            npc.dialogueTree = tree;
            npc.focusPoint = focus.transform;
            npc.indicator = indicator.transform;
            npc.talkRadius = 2f;
        }

        // ------------------------------------------------------------------ UI

        static void SetupUI(Scene scene, BuiltAssets a, Systems systems)
        {
            var root = new GameObject("Game UI");
            T Layer<T>(string name, int order) where T : Component
            {
                var go = Child(root.transform, name, Vector3.zero);
                var doc = go.AddComponent<UIDocument>();
                doc.panelSettings = a.panelSettings;
                doc.sortingOrder = order;
                return go.AddComponent<T>();
            }

            Layer<HUDController>("HUD", 10);
            Layer<TouchControls>("Touch", 15);
            var messages = Layer<GameHUD>("Messages", 11);
            var dialogue = Layer<DialogueUI>("Dialogue", 12);
            Layer<JournalUI>("Journal", 20);
            Layer<PauseMenuUI>("Pause", 30);
            Layer<EndScreenUI>("EndScreen", 40);

            systems.dialogue.dialogueUI = dialogue;
            systems.dialogue.hud = messages;
        }

        // ------------------------------------------------------------------ Cửa & chìa khóa có sẵn

        static void SetupDoorsAndKeys(Scene scene)
        {
            foreach (var door in Object.FindObjectsByType<Door>(FindObjectsInactive.Include))
            {
                bool red = door.KeyName.ToLower().Contains("red");
                door.displayName = red ? "cửa đỏ" : "cửa xanh";
                door.keyDisplayName = red ? "chìa khóa đỏ" : "chìa khóa xanh";
                if (door.GetComponent<NavMeshObstacle>() == null)
                {
                    var col = door.GetComponentInChildren<BoxCollider>();
                    var obs = door.gameObject.AddComponent<NavMeshObstacle>();
                    obs.carving = true;
                    if (col != null && col.transform == door.transform)
                    {
                        obs.shape = NavMeshObstacleShape.Box;
                        obs.size = col.size;
                        obs.center = col.center;
                    }
                }
                EditorUtility.SetDirty(door);
            }
            foreach (var key in Object.FindObjectsByType<Key>(FindObjectsInactive.Include))
            {
                key.displayName = key.KeyName.ToLower().Contains("red") ? "Chìa khóa đỏ" : "Chìa khóa xanh";
                EditorUtility.SetDirty(key);
            }
        }
    }
}
