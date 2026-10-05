using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace StealthGame.EditorTools
{
    public static partial class GameBuilder
    {
        public class BuiltAssets
        {
            public Material visionCone, shard, potion, energy, relic, sealRed, sealBlue, sealGate, stone, candle, indicator, particleAdd, particleAlpha;
            public GameObject shardPrefab, potionPrefab, energyPrefab, relicPrefab, sealPrefab, checkpointPrefab, exitGatePrefab;
            public GameObject ghostPrefab, wraithPrefab, gargoylePrefab;
            public EnemyConfig ghostConfig, wraithConfig;
            public RuntimeAnimatorController playerController, npcIdleController;
            public QuestDefinition quest;
            public DialogueTree ivanTree, whisperTree;
            public UITheme theme;
            public PanelSettings panelSettings;
        }

        const string Tex = Root + "/Art/Textures/Generated";
        const string Audio = Root + "/Audio/Generated";

        // ================================================================== Import settings

        static void ConfigureImporters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Tex }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                if (ti == null) continue;
                bool changed = ti.alphaIsTransparency != true || ti.mipmapEnabled || ti.textureCompression != TextureImporterCompression.Uncompressed;
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                if (changed) ti.SaveAndReimport();
            }

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Audio }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ai = (AudioImporter)AssetImporter.GetAtPath(path);
                bool music = path.Contains("music_");
                var s = ai.defaultSampleSettings;
                var load = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                if (s.loadType == load && ai.forceToMono) continue;
                s.loadType = load;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = music ? 0.6f : 0.8f;
                ai.defaultSampleSettings = s;
                ai.forceToMono = true;
                ai.SaveAndReimport();
            }
        }

        // ================================================================== Assets

        static BuiltAssets BuildAssets()
        {
            var a = new BuiltAssets();
            a.panelSettings = Load<PanelSettings>(Root + "/UI/PanelSettings.asset");
            // Co giãn theo chiều cao: màn hình điện thoại rất dài (20:9) vẫn giữ kích thước chữ / nút
            a.panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            a.panelSettings.referenceResolution = new Vector2Int(1280, 800);
            a.panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            a.panelSettings.match = 1f;
            EditorUtility.SetDirty(a.panelSettings);
            BuildMaterials(a);
            BuildVfx(a, out var vfxLibrary);
            a.theme = BuildTheme(a);
            BuildSoundLibrary();
            BuildEnemyConfigs(a);
            BuildPickupPrefabs(a);
            BuildEnemyPrefabs(a);
            a.playerController = BuildPlayerController();
            a.npcIdleController = BuildNpcIdleController();
            a.quest = BuildQuest();
            var trees = SampleDialogues.CreateOrUpdate(Root + "/Dialogue", overwrite: true);
            a.ivanTree = trees.ivan;
            a.whisperTree = trees.whisper;
            AssetDatabase.SaveAssets();
            return a;
        }

        /// <summary>Nạp lại mọi asset đã dựng theo đường dẫn (tham chiếu cũ bị hủy khi đổi scene).</summary>
        static BuiltAssets LoadBuilt()
        {
            Material M(string n) => AssetDatabase.LoadAssetAtPath<Material>($"{GenMat}/{n}.mat");
            GameObject P(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{GenPrefab}/{n}.prefab");
            return new BuiltAssets
            {
                visionCone = M("VisionCone"), shard = M("Shard"), potion = M("Potion"), energy = M("Energy"), relic = M("Relic"),
                sealRed = M("SealRed"), sealBlue = M("SealBlue"), sealGate = M("SealGate"), stone = M("Stone"), candle = M("Candle"),
                indicator = M("NPC_Indicator"), particleAdd = M("ParticleAdditive"), particleAlpha = M("ParticleAlpha"),
                shardPrefab = P("Pickup_Shard"), potionPrefab = P("Pickup_Potion"), energyPrefab = P("Pickup_Energy"), relicPrefab = P("Relic"),
                sealPrefab = P("KeySeal"), checkpointPrefab = P("Checkpoint"), exitGatePrefab = P("ExitGate"),
                ghostPrefab = P("Enemy_Ghost"), wraithPrefab = P("Enemy_Wraith"), gargoylePrefab = P("Enemy_Gargoyle"),
                ghostConfig = AssetDatabase.LoadAssetAtPath<EnemyConfig>(GenData + "/Enemy_Ghost.asset"),
                wraithConfig = AssetDatabase.LoadAssetAtPath<EnemyConfig>(GenData + "/Enemy_Wraith.asset"),
                playerController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Gen + "/Player.controller"),
                npcIdleController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Gen + "/NPC_Idle.controller"),
                quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(GenData + "/MainQuest.asset"),
                ivanTree = AssetDatabase.LoadAssetAtPath<DialogueTree>(Root + "/Dialogue/Dialogue_Ivan.asset"),
                whisperTree = AssetDatabase.LoadAssetAtPath<DialogueTree>(Root + "/Dialogue/Dialogue_Whisper.asset"),
                theme = AssetDatabase.LoadAssetAtPath<UITheme>(ResourcesDir + "/UITheme.asset"),
                panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(Root + "/UI/PanelSettings.asset"),
            };
        }

        // ------------------------------------------------------------------ Materials

        static Material Mat(string name, string shaderName, Color color, Color? emission = null, bool transparent = false, bool additive = false, Texture tex = null)
        {
            string path = $"{GenMat}/{name}.mat";
            var shader = Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", additive ? 2f : 0f);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            }
            else if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);

            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            BaseShaderGUI.SetMaterialKeywords(m);
            if (emission.HasValue) m.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        static void BuildMaterials(BuiltAssets a)
        {
            const string lit = "Universal Render Pipeline/Lit";
            const string unlit = "Universal Render Pipeline/Unlit";
            const string particles = "Universal Render Pipeline/Particles/Unlit";
            var soft = Load<Texture2D>(Tex + "/fx_soft_particle.png");

            a.visionCone = Mat("VisionCone", unlit, new Color(1f, 1f, 1f, 0.15f), transparent: true);
            a.shard = Mat("Shard", lit, new Color(0.5f, 0.9f, 1f), new Color(0.3f, 1.4f, 2.0f));
            a.potion = Mat("Potion", lit, new Color(0.9f, 0.15f, 0.2f), new Color(1.2f, 0.1f, 0.15f));
            a.energy = Mat("Energy", lit, new Color(1f, 0.85f, 0.3f), new Color(1.6f, 1.1f, 0.2f));
            a.relic = Mat("Relic", lit, new Color(1f, 0.8f, 0.35f), new Color(2.2f, 1.5f, 0.4f));
            a.sealRed = Mat("SealRed", unlit, new Color(1f, 0.15f, 0.1f, 0.35f), transparent: true, additive: true);
            a.sealBlue = Mat("SealBlue", unlit, new Color(0.2f, 0.45f, 1f, 0.35f), transparent: true, additive: true);
            a.sealGate = Mat("SealGate", unlit, new Color(1f, 0.75f, 0.25f, 0.4f), transparent: true, additive: true);
            a.stone = Mat("Stone", lit, new Color(0.22f, 0.2f, 0.24f));
            a.candle = Mat("Candle", lit, new Color(0.85f, 0.85f, 0.8f), new Color(0.15f, 0.2f, 0.3f));
            a.indicator = Mat("NPC_Indicator", unlit, new Color(1f, 0.82f, 0.2f));
            a.particleAdd = Mat("ParticleAdditive", particles, Color.white, transparent: true, additive: true, tex: soft);
            a.particleAlpha = Mat("ParticleAlpha", particles, Color.white, transparent: true, additive: false, tex: soft);
        }

        // ------------------------------------------------------------------ VFX

        static GameObject Particles(string name, Material mat, System.Action<ParticleSystem> setup)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            setup(ps);
            return go;
        }

        static void Burst(ParticleSystem ps, int count) => ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        static void Fade(ParticleSystem ps, Color start, Color end, bool shrink = true)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            if (shrink)
            {
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
            }
        }

        static void Basic(ParticleSystem ps, float lifeMin, float lifeMax, float speedMin, float speedMax, float sizeMin, float sizeMax, Color color, float gravity = 0f)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.duration = lifeMax;
        }

        static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius, float angle = 25f)
        {
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = type;
            sh.radius = radius;
            sh.angle = angle;
        }

        static GameObject Ring(Transform parent, Material mat, Color color, float endSize, float life)
        {
            var ring = Particles("Ring", mat, ps =>
            {
                Basic(ps, life, life, 0f, 0f, 0.3f, 0.3f, color);
                Burst(ps, 1);
                var sh = ps.shape; sh.enabled = false;
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(endSize / 0.3f, AnimationCurve.EaseInOut(0f, 0.1f, 1f, 1f));
                Fade(ps, color, color, false);
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            });
            ring.transform.SetParent(parent, false);
            return ring;
        }

        static void BuildVfx(BuiltAssets a, out VFXLibrary library)
        {
            var ringTex = Load<Texture2D>(Tex + "/fx_ring.png");
            var ringMat = Mat("ParticleRing", "Universal Render Pipeline/Particles/Unlit", Color.white, transparent: true, additive: true, tex: ringTex);
            var add = a.particleAdd;
            var entries = new List<VFXLibrary.Entry>();

            void Save(VFXId id, GameObject go, float life)
            {
                var prefab = SavePrefab(go, $"{GenVfx}/VFX_{id}.prefab");
                entries.Add(new VFXLibrary.Entry { id = id, prefab = prefab, lifetime = life });
            }

            Save(VFXId.PickupSparkle, Particles("PickupSparkle", add, ps =>
            {
                Basic(ps, 0.4f, 0.9f, 1.2f, 3f, 0.05f, 0.14f, Color.white, -0.15f);
                Shape(ps, ParticleSystemShapeType.Sphere, 0.15f);
                Burst(ps, 24);
                Fade(ps, Color.white, Color.white);
            }), 1.2f);

            Save(VFXId.LanternBurst, Particles("LanternBurst", add, ps =>
            {
                Basic(ps, 0.3f, 0.6f, 6f, 10f, 0.15f, 0.4f, new Color(1f, 0.9f, 0.6f));
                Shape(ps, ParticleSystemShapeType.Cone, 0.15f, 35f);
                Burst(ps, 70);
                Fade(ps, new Color(1f, 0.95f, 0.7f), new Color(1f, 0.6f, 0.2f));
            }), 0.9f);

            Save(VFXId.EnemyHit, Particles("EnemyHit", add, ps =>
            {
                Basic(ps, 0.3f, 0.6f, 2f, 4.5f, 0.06f, 0.16f, new Color(0.7f, 0.9f, 1f));
                Shape(ps, ParticleSystemShapeType.Sphere, 0.3f);
                Burst(ps, 30);
                Fade(ps, Color.white, new Color(0.4f, 0.7f, 1f));
            }), 0.8f);

            var banish = Particles("EnemyBanish", add, ps =>
            {
                Basic(ps, 1.0f, 1.8f, 0.4f, 2.2f, 0.1f, 0.3f, new Color(0.6f, 0.95f, 1f), -0.4f);
                Shape(ps, ParticleSystemShapeType.Sphere, 0.5f);
                Burst(ps, 80);
                Fade(ps, new Color(0.8f, 1f, 1f), new Color(0.3f, 0.5f, 1f));
            });
            Ring(banish.transform, ringMat, new Color(0.6f, 0.9f, 1f), 3.5f, 0.6f);
            Save(VFXId.EnemyBanish, banish, 2f);

            Save(VFXId.PlayerHit, Particles("PlayerHit", add, ps =>
            {
                Basic(ps, 0.3f, 0.6f, 2f, 4f, 0.06f, 0.15f, new Color(1f, 0.2f, 0.15f), 0.5f);
                Shape(ps, ParticleSystemShapeType.Sphere, 0.25f);
                Burst(ps, 26);
                Fade(ps, new Color(1f, 0.3f, 0.2f), new Color(0.5f, 0f, 0f));
            }), 0.8f);

            var cp = Particles("Checkpoint", add, ps =>
            {
                Basic(ps, 0.8f, 1.4f, 0.5f, 2f, 0.08f, 0.2f, new Color(0.5f, 0.75f, 1f), -0.6f);
                Shape(ps, ParticleSystemShapeType.Circle, 0.4f);
                Burst(ps, 50);
                Fade(ps, new Color(0.7f, 0.9f, 1f), new Color(0.2f, 0.4f, 1f));
            });
            cp.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Ring(cp.transform, ringMat, new Color(0.5f, 0.8f, 1f), 3f, 0.7f);
            Save(VFXId.Checkpoint, cp, 1.8f);

            var relic = Particles("RelicCollect", add, ps =>
            {
                Basic(ps, 0.8f, 1.5f, 1.5f, 5f, 0.08f, 0.25f, new Color(1f, 0.8f, 0.3f), -0.2f);
                Shape(ps, ParticleSystemShapeType.Sphere, 0.3f);
                Burst(ps, 100);
                Fade(ps, new Color(1f, 0.95f, 0.6f), new Color(1f, 0.5f, 0.1f));
            });
            Ring(relic.transform, ringMat, new Color(1f, 0.8f, 0.3f), 4f, 0.8f);
            Save(VFXId.RelicCollect, relic, 2f);

            var seal = Particles("SealBreak", add, ps =>
            {
                Basic(ps, 0.6f, 1.2f, 3f, 7f, 0.08f, 0.22f, Color.white);
                Shape(ps, ParticleSystemShapeType.Sphere, 0.8f);
                Burst(ps, 120);
                Fade(ps, Color.white, new Color(1f, 0.7f, 0.3f));
            });
            Ring(seal.transform, ringMat, Color.white, 5f, 0.7f);
            Save(VFXId.SealBreak, seal, 1.8f);

            var dust = Particles("LandDust", a.particleAlpha, ps =>
            {
                Basic(ps, 0.4f, 0.7f, 0.6f, 1.6f, 0.15f, 0.35f, new Color(0.6f, 0.55f, 0.5f, 0.5f));
                Shape(ps, ParticleSystemShapeType.Circle, 0.25f);
                Burst(ps, 14);
                Fade(ps, new Color(0.6f, 0.55f, 0.5f), new Color(0.4f, 0.4f, 0.4f), false);
            });
            dust.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Save(VFXId.LandDust, dust, 0.9f);

            var heal = Particles("Heal", add, ps =>
            {
                Basic(ps, 0.7f, 1.2f, 0.4f, 1.4f, 0.06f, 0.14f, new Color(0.4f, 1f, 0.5f), -0.5f);
                Shape(ps, ParticleSystemShapeType.Circle, 0.45f);
                Burst(ps, 36);
                Fade(ps, new Color(0.6f, 1f, 0.6f), new Color(0.2f, 0.8f, 0.3f));
            });
            heal.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Save(VFXId.Heal, heal, 1.4f);

            library = LoadOrCreate<VFXLibrary>(ResourcesDir + "/VFXLibrary.asset");
            library.entries = entries;
            EditorUtility.SetDirty(library);
        }

        // ------------------------------------------------------------------ UI theme

        static UITheme BuildTheme(BuiltAssets a)
        {
            var t = LoadOrCreate<UITheme>(ResourcesDir + "/UITheme.asset");
            t.panelSettings = a.panelSettings;
            t.titleFont = Load<Font>(Root + "/Art/Fonts/IrishGrover-Regular.ttf");
            t.bodyFont = null;   // font động LegacyRuntime – hỗ trợ tiếng Việt
            Texture2D I(string n) => Load<Texture2D>($"{Tex}/icon_{n}.png");
            t.heart = I("heart"); t.stamina = I("stamina"); t.lantern = I("lantern"); t.shard = I("shard");
            t.potion = I("potion"); t.relic = I("relic"); t.key = I("key"); t.ghost = I("ghost"); t.clock = I("clock");
            t.skull = I("skull"); t.eye = I("eye"); t.quest = I("quest"); t.checkpoint = I("checkpoint"); t.trophy = I("trophy");
            t.energy = I("energy");
            t.wonScreen = Load<Texture2D>(Root + "/Art/Textures/UI/Won Screen.png");
            t.caughtScreen = Load<Texture2D>(Root + "/Art/Textures/UI/Caught Screen.png");
            t.vignette = Load<Texture2D>(Tex + "/fx_vignette.png");
            EditorUtility.SetDirty(t);
            return t;
        }

        // ------------------------------------------------------------------ Sound library

        static void BuildSoundLibrary()
        {
            var lib = LoadOrCreate<SoundLibrary>(ResourcesDir + "/SoundLibrary.asset");
            var list = new List<SoundLibrary.Entry>();

            void Add(SoundId id, float volume, float pitch, params string[] files)
            {
                var clips = files.Select(f => AssetDatabase.LoadAssetAtPath<AudioClip>(f.StartsWith("Assets") ? f : $"{Audio}/{f}.wav"))
                                 .Where(c => c != null).ToArray();
                if (clips.Length == 0) Debug.LogWarning("[GameBuilder] Không tìm thấy âm thanh cho " + id);
                list.Add(new SoundLibrary.Entry { id = id, clips = clips, volume = volume, pitchVariance = pitch });
            }

            Add(SoundId.MusicMenu, 0.8f, 0f, "music_menu");
            Add(SoundId.MusicExplore, 0.75f, 0f, "music_explore");
            Add(SoundId.MusicChase, 0.7f, 0f, "music_chase");
            Add(SoundId.Jump, 0.6f, 0.08f, "sfx_jump");
            Add(SoundId.Land, 0.7f, 0.1f, "sfx_land");
            Add(SoundId.Hurt, 0.9f, 0.08f, "sfx_hurt");
            Add(SoundId.PlayerDeath, 1f, 0f, "sfx_player_death");
            Add(SoundId.Footstep, 0.5f, 0.15f, "sfx_footstep");
            Add(SoundId.Heartbeat, 0.8f, 0f, "sfx_heartbeat");
            Add(SoundId.LanternBurst, 0.8f, 0.06f, "sfx_lantern_burst");
            Add(SoundId.LanternToggle, 0.6f, 0.05f, "sfx_lantern_toggle");
            Add(SoundId.EnergyEmpty, 0.7f, 0f, "sfx_empty");
            Add(SoundId.GhostHit, 0.8f, 0.1f, "sfx_ghost_hit");
            Add(SoundId.GhostBanish, 0.9f, 0.05f, "sfx_ghost_banish");
            Add(SoundId.GhostAttack, 0.8f, 0.1f, "sfx_ghost_attack");
            Add(SoundId.GhostAlert, 0.9f, 0.05f, "sfx_ghost_alert");
            Add(SoundId.GhostMove, 0.5f, 0.1f, Root + "/Audio/SFXGhostMove.wav");
            Add(SoundId.Alarm, 0.8f, 0f, "sfx_alarm");
            Add(SoundId.Shard, 0.6f, 0.12f, "sfx_shard");
            Add(SoundId.Potion, 0.8f, 0.05f, "sfx_potion");
            Add(SoundId.EnergyPickup, 0.8f, 0.05f, "sfx_energy");
            Add(SoundId.Key, 0.9f, 0f, "sfx_key");
            Add(SoundId.Relic, 1f, 0f, "sfx_relic");
            Add(SoundId.Checkpoint, 0.8f, 0f, "sfx_checkpoint");
            Add(SoundId.DoorOpen, 0.8f, 0.05f, "sfx_door");
            Add(SoundId.Locked, 0.8f, 0.05f, "sfx_locked");
            Add(SoundId.UIClick, 0.6f, 0.03f, "sfx_ui_click");
            Add(SoundId.UIHover, 0.4f, 0.05f, "sfx_ui_hover");
            Add(SoundId.UIOpen, 0.5f, 0f, "sfx_ui_open");
            Add(SoundId.Victory, 1f, 0f, "sfx_victory", Root + "/Audio/SFXWin.wav");
            Add(SoundId.GameOver, 1f, 0f, "sfx_gameover");

            lib.entries = list;
            EditorUtility.SetDirty(lib);
        }

        // ------------------------------------------------------------------ Enemy configs

        static void BuildEnemyConfigs(BuiltAssets a)
        {
            a.ghostConfig = LoadOrCreate<EnemyConfig>(GenData + "/Enemy_Ghost.asset");
            var g = a.ghostConfig;
            g.displayName = "Hồn ma";
            g.maxHealth = 100f; g.scoreValue = 200; g.shardDrop = 2;
            g.patrolSpeed = 1.1f; g.chaseSpeed = 2.7f; g.waitAtWaypoint = 1.5f;
            g.visionRange = 6.5f; g.visionAngle = 100f; g.detectionRate = 0.95f; g.awarenessDecay = 0.35f;
            g.hearingMultiplier = 1f; g.loseSightTime = 4f; g.investigateTime = 4f;
            g.attackRange = 1.2f; g.attackDamage = 20f; g.attackWindup = 0.45f; g.attackCooldown = 1.3f;
            EditorUtility.SetDirty(g);

            a.wraithConfig = LoadOrCreate<EnemyConfig>(GenData + "/Enemy_Wraith.asset");
            var w = a.wraithConfig;
            w.displayName = "Oan hồn";
            w.maxHealth = 220f; w.scoreValue = 600; w.shardDrop = 5;
            w.patrolSpeed = 1.3f; w.chaseSpeed = 3.1f; w.waitAtWaypoint = 1f;
            w.visionRange = 8f; w.visionAngle = 120f; w.detectionRate = 1.35f; w.awarenessDecay = 0.25f;
            w.hearingMultiplier = 1.4f; w.loseSightTime = 6f; w.investigateTime = 6f;
            w.attackRange = 1.35f; w.attackDamage = 30f; w.attackWindup = 0.4f; w.attackCooldown = 1.1f;
            w.modelScale = 1.3f;
            EditorUtility.SetDirty(w);
        }

        // ------------------------------------------------------------------ Pickups & world prefabs

        static void BuildPickupPrefabs(BuiltAssets a)
        {
            GameObject Base(string name, PickupType type, int amount, float radius, string display)
            {
                var go = new GameObject(name);
                var col = go.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = radius;
                col.center = new Vector3(0f, 0.5f, 0f);
                var p = go.AddComponent<Pickup>();
                p.type = type;
                p.amount = amount;
                p.displayName = display;
                var visual = Child(go.transform, "Visual", new Vector3(0f, 0.5f, 0f));
                p.visual = visual.transform;
                return go;
            }

            // Mảnh vụn linh hồn
            var shard = Base("Pickup_Shard", PickupType.Shard, 1, 0.45f, "Mảnh vụn linh hồn");
            var gem = Primitive(PrimitiveType.Cube, "Gem", shard.transform.Find("Visual"), Vector3.zero, Vector3.one * 0.16f, a.shard);
            gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            a.shardPrefab = SavePrefab(shard, GenPrefab + "/Pickup_Shard.prefab");

            // Bình máu
            var potion = Base("Pickup_Potion", PickupType.Potion, 1, 0.5f, "Bình máu");
            var pv = potion.transform.Find("Visual");
            Primitive(PrimitiveType.Sphere, "Flask", pv, Vector3.zero, Vector3.one * 0.24f, a.potion);
            Primitive(PrimitiveType.Cylinder, "Neck", pv, new Vector3(0f, 0.15f, 0f), new Vector3(0.08f, 0.06f, 0.08f), a.candle);
            AddLight(pv, "Glow", Vector3.zero, LightType.Point, new Color(1f, 0.3f, 0.35f), 1.6f, 1.5f);
            potion.GetComponent<Pickup>().spinSpeed = 45f;
            a.potionPrefab = SavePrefab(potion, GenPrefab + "/Pickup_Potion.prefab");

            // Dầu đèn (năng lượng)
            var energy = Base("Pickup_Energy", PickupType.Energy, 1, 0.5f, "Dầu đèn");
            var ev = energy.transform.Find("Visual");
            Primitive(PrimitiveType.Capsule, "Oil", ev, Vector3.zero, new Vector3(0.16f, 0.13f, 0.16f), a.energy);
            AddLight(ev, "Glow", Vector3.zero, LightType.Point, new Color(1f, 0.8f, 0.3f), 1.6f, 1.5f);
            a.energyPrefab = SavePrefab(energy, GenPrefab + "/Pickup_Energy.prefab");

            // Mảnh hồn (đặt trên bệ đá)
            var relic = new GameObject("Relic");
            var relicCol = relic.AddComponent<SphereCollider>();
            relicCol.isTrigger = true;
            relicCol.radius = 0.85f;
            relicCol.center = new Vector3(0f, 1.0f, 0f);
            var rp = relic.AddComponent<Pickup>();
            rp.type = PickupType.Relic;
            rp.magnetRadius = 0f;
            rp.bobHeight = 0.08f;
            rp.spinSpeed = 60f;
            var rv = Child(relic.transform, "Visual", new Vector3(0f, 1.15f, 0f));
            rp.visual = rv.transform;
            var g1 = Primitive(PrimitiveType.Cube, "Gem", rv.transform, Vector3.zero, Vector3.one * 0.28f, a.relic);
            g1.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            var g2 = Primitive(PrimitiveType.Cube, "Gem2", rv.transform, Vector3.zero, Vector3.one * 0.2f, a.relic);
            g2.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            AddLight(rv.transform, "Glow", Vector3.zero, LightType.Point, new Color(1f, 0.75f, 0.3f), 4f, 3f);
            var aura = Particles("Aura", a.particleAdd, ps =>
            {
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.duration = 2f;
                Basic(ps, 1f, 1.8f, 0.1f, 0.4f, 0.04f, 0.1f, new Color(1f, 0.8f, 0.3f), -0.15f);
                main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                var em = ps.emission;
                em.rateOverTime = 14f;
                Shape(ps, ParticleSystemShapeType.Sphere, 0.35f);
                Fade(ps, new Color(1f, 0.9f, 0.5f), new Color(1f, 0.6f, 0.2f));
            });
            aura.transform.SetParent(rv.transform, false);
            a.relicPrefab = SavePrefab(relic, GenPrefab + "/Relic.prefab");

            // Phong ấn (màu đặt khi dựng scene)
            var seal = new GameObject("KeySeal");
            var sealCol = seal.AddComponent<CapsuleCollider>();
            sealCol.radius = 0.95f;
            sealCol.height = 3f;
            sealCol.center = new Vector3(0f, 1.3f, 0f);
            var obstacle = seal.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = 0.95f;
            obstacle.height = 2.6f;
            obstacle.center = new Vector3(0f, 1.3f, 0f);
            obstacle.carving = true;
            var ks = seal.AddComponent<KeySeal>();
            var barrier = Primitive(PrimitiveType.Cylinder, "Barrier", seal.transform, new Vector3(0f, 1.3f, 0f), new Vector3(1.9f, 1.3f, 1.9f), a.sealRed);
            barrier.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ks.barrier = barrier.transform;
            a.sealPrefab = SavePrefab(seal, GenPrefab + "/KeySeal.prefab");

            // Checkpoint: bệ đá + nến + ngọn lửa xanh
            var cp = new GameObject("Checkpoint");
            var cpCol = cp.AddComponent<SphereCollider>();
            cpCol.isTrigger = true;
            cpCol.radius = 1.3f;
            cpCol.center = new Vector3(0f, 0.6f, 0f);
            var c = cp.AddComponent<Checkpoint>();
            var basePart = Primitive(PrimitiveType.Cylinder, "Base", cp.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.55f, 0.2f, 0.55f), a.stone, keepCollider: true);
            Primitive(PrimitiveType.Cylinder, "Candle", cp.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.14f, 0.2f, 0.14f), a.candle);
            var flameRoot = Child(cp.transform, "Flame", new Vector3(0f, 0.85f, 0f));
            var candleFlame = Load<GameObject>(Root + "/Prefabs/VFX/CandleFlame.prefab");
            if (candleFlame != null)
            {
                var f = (GameObject)PrefabUtility.InstantiatePrefab(candleFlame, flameRoot.transform);
                f.transform.localPosition = Vector3.zero;
            }
            else Primitive(PrimitiveType.Sphere, "FlameBall", flameRoot.transform, Vector3.zero, Vector3.one * 0.12f, a.energy);
            c.flame = flameRoot;
            c.flameLight = AddLight(cp.transform, "FlameLight", new Vector3(0f, 1.1f, 0f), LightType.Point, new Color(0.55f, 0.75f, 1f), 5f, 2.5f);
            c.spawnPoint = Child(cp.transform, "Spawn", new Vector3(0f, 0f, 1.0f)).transform;
            var cpObstacle = basePart.AddComponent<NavMeshObstacle>();
            cpObstacle.carving = true;
            a.checkpointPrefab = SavePrefab(cp, GenPrefab + "/Checkpoint.prefab");

            // Cổng thoát
            var gate = new GameObject("ExitGate");
            var gateCol = gate.AddComponent<BoxCollider>();
            gateCol.size = new Vector3(0.5f, 2.8f, 3.6f);
            gateCol.center = new Vector3(0f, 1.4f, 0f);
            var gateObs = gate.AddComponent<NavMeshObstacle>();
            gateObs.shape = NavMeshObstacleShape.Box;
            gateObs.size = gateCol.size;
            gateObs.center = gateCol.center;
            gateObs.carving = true;
            var eg = gate.AddComponent<ExitGate>();
            var gb = Primitive(PrimitiveType.Cube, "Barrier", gate.transform, new Vector3(0f, 1.4f, 0f), new Vector3(0.25f, 2.8f, 3.6f), a.sealGate);
            gb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            eg.barrier = gb.transform;
            AddLight(gate.transform, "GateGlow", new Vector3(-0.6f, 1.5f, 0f), LightType.Point, new Color(1f, 0.75f, 0.3f), 5f, 2f);
            var zone = Child(gate.transform, "ExitZone", new Vector3(1.6f, 1f, 0f));
            var zc = zone.AddComponent<BoxCollider>();
            zc.isTrigger = true;
            zc.size = new Vector3(2f, 2f, 3f);
            zone.AddComponent<ExitZone>();
            eg.exitZone = zone;
            a.exitGatePrefab = SavePrefab(gate, GenPrefab + "/ExitGate.prefab");
        }

        // ------------------------------------------------------------------ Enemy prefabs

        static GameObject AddVisionCone(Transform parent, Material mat)
        {
            var go = Child(parent, "VisionCone", new Vector3(0f, 0.02f, 0f));
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            go.AddComponent<VisionCone>();
            return go;
        }

        static void StripLegacy(GameObject go)
        {
            foreach (var o in go.GetComponentsInChildren<Observer>(true))
                Object.DestroyImmediate(o.gameObject);
            foreach (var w in go.GetComponentsInChildren<WaypointPatrol>(true))
                Object.DestroyImmediate(w);
        }

        static GameObject MakeGhost(BuiltAssets a, string name, EnemyConfig config, Color? tint)
        {
            var src = Load<GameObject>(Root + "/Tutorial_Demo/Demo_Prefabs/Demo_Ghost.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            StripLegacy(go);
            go.transform.position = Vector3.zero;

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.25f;
            agent.height = 1.4f;
            agent.speed = config.patrolSpeed;
            agent.angularSpeed = 400f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 0.3f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            agent.enabled = false;   // EnemyAI bật lại khi NavMesh của scene đã nạp

            var animator = go.GetComponentInChildren<Animator>();
            var ai = go.AddComponent<EnemyAI>();
            ai.config = config;
            ai.animator = animator;
            ai.model = animator != null ? animator.transform : null;
            if (ai.model != null && ai.model != go.transform)
                ai.model.localScale *= config.modelScale;
            ai.visionCone = AddVisionCone(go.transform, a.visionCone).GetComponent<VisionCone>();

            foreach (var src2 in go.GetComponentsInChildren<AudioSource>())
                if (src2.GetComponent<AudioVolumeBinder>() == null) src2.gameObject.AddComponent<AudioVolumeBinder>().category = SoundCategory.Sfx;

            if (tint.HasValue)
            {
                var wraithMat = Mat("WraithBody", "Universal Render Pipeline/Lit", tint.Value, new Color(0.6f, 0.05f, 0.05f));
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (r.GetComponent<VisionCone>() != null) continue;
                    var src3 = r.sharedMaterial;
                    if (src3 != null && src3.HasProperty("_BaseMap") && src3.GetTexture("_BaseMap") != null)
                        wraithMat.SetTexture("_BaseMap", src3.GetTexture("_BaseMap"));
                    r.sharedMaterials = Enumerable.Repeat(wraithMat, r.sharedMaterials.Length).ToArray();
                }
            }
            return SavePrefab(go, $"{GenPrefab}/{name}.prefab");
        }

        static void BuildEnemyPrefabs(BuiltAssets a)
        {
            a.ghostPrefab = MakeGhost(a, "Enemy_Ghost", a.ghostConfig, null);
            a.wraithPrefab = MakeGhost(a, "Enemy_Wraith", a.wraithConfig, new Color(0.55f, 0.15f, 0.2f));

            // Tượng Gargoyle canh gác
            var src = Load<GameObject>(Root + "/Tutorial_Demo/Demo_Prefabs/Demo_Gargoyle.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Enemy_Gargoyle";
            StripLegacy(go);
            var sentry = go.AddComponent<SentryAI>();
            sentry.visionCone = AddVisionCone(go.transform, a.visionCone).GetComponent<VisionCone>();
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = 0.35f;
            obstacle.height = 1.8f;
            obstacle.center = new Vector3(0f, 0.9f, 0f);
            obstacle.carving = true;
            a.gargoylePrefab = SavePrefab(go, GenPrefab + "/Enemy_Gargoyle.prefab");
        }

        // ------------------------------------------------------------------ Animator

        static AnimationClip Clip(string fbx)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        static RuntimeAnimatorController BuildPlayerController()
        {
            string path = Gen + "/Player.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);

            var idle = Clip(Root + "/Art/Animation/Animations/Character@Idle1.fbx");
            var walk = Clip(Root + "/Art/Animation/Animations/Character@Walk1.fbx");
            controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1f);
            tree.AddChild(walk, 2f);
            var children = tree.children;
            children[2].timeScale = 1.75f;   // "chạy" = đi bộ tăng tốc
            tree.children = children;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static RuntimeAnimatorController BuildNpcIdleController()
        {
            string path = Gen + "/NPC_Idle.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var clip = Clip(Root + "/Art/Animation/Animations/Character@Idle1.fbx");
            if (clip != null) controller.AddMotion(clip);
            return controller;
        }

        // ------------------------------------------------------------------ Quest

        static QuestDefinition BuildQuest()
        {
            var q = LoadOrCreate<QuestDefinition>(GenData + "/MainQuest.asset");
            q.title = "Thoát khỏi dinh thự ma ám";
            q.story =
                "Đêm ấy, John Lemon tỉnh dậy trong một dinh thự cổ bị nguyền rủa. Dinh thự chỉ tồn tại trong bóng đêm – " +
                "khi bình minh ló dạng nó sẽ tan biến cùng mọi linh hồn còn mắc kẹt bên trong.\n\n" +
                "Cửa chính bị phong ấn bởi ba Mảnh hồn. Lũ ma tuần tra khắp các hành lang, tượng Gargoyle canh gác mọi lối đi, " +
                "và một Oan hồn đáng sợ trú ngụ trong phòng ăn. Với chiếc đèn lồng linh hồn trong tay, John phải lén lút thu thập " +
                "đủ ba mảnh và thoát ra trước 6 giờ sáng.";
            q.objectives = new List<QuestObjective>
            {
                new() { id = "talk_ivan", text = "Nói chuyện với Ivan trong phòng ngủ", type = ObjectiveType.Flag, key = "ivan_met",
                        hint = "Ivan đứng ngay cạnh bạn lúc bắt đầu. Nhấn E để nói chuyện." },
                new() { id = "relics", text = "Thu thập Mảnh hồn ({0}/{1})", type = ObjectiveType.RelicCount, target = 3, requires = "talk_ivan",
                        hint = "Bếp (gần phòng ngủ) • Thư phòng phía Bắc (Phong ấn Đỏ) • Phòng ăn ô cờ (Phong ấn Xanh, có Oan hồn)." },
                new() { id = "key_red", text = "Tìm chìa khóa đỏ ở Cánh Tây", type = ObjectiveType.HasKey, key = "keyRed", requires = "talk_ivan",
                        hint = "Căn phòng xanh cuối hành lang phía Tây. Cẩn thận tượng Gargoyle ở hành lang." },
                new() { id = "key_blue", text = "Tìm chìa khóa xanh ở phòng ô cờ giữa nhà", type = ObjectiveType.HasKey, key = "keyBlue", requires = "talk_ivan",
                        hint = "Căn phòng nhỏ lát gạch ô cờ ở giữa dinh thự." },
                new() { id = "gate", text = "Phá phong ấn cửa chính ở Cánh Đông", type = ObjectiveType.Opened, key = ExitGate.GateId, requires = "relics",
                        hint = "Cuối hành lang phía Đông. Nhấn E trước cánh cổng vàng." },
                new() { id = "escape", text = "Thoát ra ngoài!", type = ObjectiveType.Escaped, requires = "gate" },
                new() { id = "banish", text = "Trục xuất hồn ma ({0}/{1})", type = ObjectiveType.BanishCount, target = 3, optional = true, requires = "talk_ivan",
                        hint = "Dùng Linh Quang (chuột trái). Đánh từ phía sau gây sát thương x2." },
                new() { id = "shards", text = "Thu thập mảnh vụn linh hồn ({0}/{1})", type = ObjectiveType.ShardCount, target = 20, optional = true, requires = "talk_ivan",
                        hint = "Những viên pha lê xanh nhỏ khắp dinh thự. Đổi với Ivan lấy bình máu / dầu đèn." },
            };
            EditorUtility.SetDirty(q);
            return q;
        }
    }
}
