using kcp2k;
using Party.Juice;
using UnityEngine.Rendering.Universal;
using Mirror;
using Party.RedLight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Party.EditorTools
{
    /// <summary>
    /// Builds the "Red Light, Barnaby" scene from code.
    ///
    /// A SEPARATE scene from NetTest on purpose. NetTest is the netcode proof and
    /// Tests/netcode_sync_test.sh asserts against it; adding gameplay there would mean
    /// the regression test no longer measures netcode in isolation.
    ///
    ///     Unity -batchmode -quit -executeMethod Party.EditorTools.RedLightSetup.Build
    /// </summary>
    public static class RedLightSetup
    {
        const string ScenePath    = "Assets/_Party/Scenes/RedLight.unity";
        const string PrefabPath   = "Assets/_Party/Prefabs/PartyPlayer.prefab";
        const string DirectorPath = "Assets/_Party/Prefabs/RedLightDirector.prefab";

        /// <summary>
        /// The director is a PREFAB spawned by the server, never a scene object.
        ///
        /// Mirror's build-time scene post-processor disables any scene object carrying a
        /// NetworkIdentity so the server can spawn it on demand. Putting the director on
        /// the NetworkManager object therefore disabled the whole networking object in
        /// the build - silently, with no error in the player, while still working in the
        /// editor. Mirror warns about this at build time; heed it.
        /// </summary>
        static GameObject BuildDirectorPrefab()
        {
            GameObject go = new GameObject("RedLightDirector");
            go.AddComponent<NetworkIdentity>();
            go.AddComponent<HostVoice>();
            RedLightDirector d = go.AddComponent<RedLightDirector>();
            d.startZ = -46f;
            d.finishZ = 46f;

            System.IO.Directory.CreateDirectory("Assets/_Party/Prefabs");
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, DirectorPath);
            Object.DestroyImmediate(go);
            return asset;
        }

        [MenuItem("Party/Rebuild Red Light scene")]
        public static void Build()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError("[Party] player prefab missing - run Party/Rebuild milestone scene first.");
                EditorApplication.Exit(1);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            const float HalfWidth = 9f;
            const float StartZ = -46f, FinishZ = 46f;

            // A long lane rather than an open plane: Red Light needs a direction to run in.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Lane";
            ground.transform.localScale = new Vector3(1.8f, 1f, 10.4f);   // ~18 x 104 units
            ground.GetComponent<Renderer>().sharedMaterial =
                PresentationSetup.Lit("Lane", new Color(0.30f, 0.33f, 0.40f), 0.18f);

            MakeStripe("StartLine",  StartZ,  new Color(0.90f, 0.92f, 0.96f));
            MakeStripe("FinishLine", FinishZ, new Color(0.98f, 0.80f, 0.25f));

            PresentationSetup.Barriers(HalfWidth, StartZ - 6f, FinishZ + 6f);
            PresentationSetup.FinishGantry(FinishZ, HalfWidth);
            PresentationSetup.Crowd(HalfWidth, StartZ - 4f, FinishZ + 4f);
            BuildHazards(HalfWidth, StartZ, FinishZ);

            GameObject lightGo = new GameObject("Directional Light");
            lightGo.AddComponent<Light>().type = LightType.Directional;
            PresentationSetup.Lighting(lightGo);
            PresentationSetup.GlobalVolume();

            // Broadcast camera: follows the leader, punches on STOP, pushes in on the win.
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 52f;
            camGo.transform.position = new Vector3(0f, 13f, StartZ - 14f);
            camGo.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraRig>();

            UniversalAdditionalCameraData cd = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (cd == null) cd = camGo.AddComponent<UniversalAdditionalCameraData>();
            cd.renderPostProcessing = true;   // without this the volume does nothing
            cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            GameObject netGo = new GameObject("NetworkManager");
            KcpTransport kcp = netGo.AddComponent<KcpTransport>();
            Mirror.FizzySteam.FizzySteamworks fizzy = netGo.AddComponent<Mirror.FizzySteam.FizzySteamworks>();
            netGo.AddComponent<SteamBoot>();

            PartyNetworkManager nm = netGo.AddComponent<PartyNetworkManager>();
            nm.transport = kcp;
            nm.localTransport = kcp;
            nm.steamTransport = fizzy;
            nm.playerPrefab = playerPrefab;
            nm.targetParticipants = 4;
            nm.headlessStartMode = HeadlessStartOptions.DoNothing;

            netGo.AddComponent<SteamLobby>();
            netGo.AddComponent<PartyHUD>();
            netGo.AddComponent<RedLightHUD>();
            netGo.AddComponent<MilestoneAutoRun>();

            // Director as a server-spawned prefab. NO NetworkIdentity on this object.
            GameObject directorPrefab = BuildDirectorPrefab();
            nm.serverSpawnOnStart = new[] { directorPrefab };
            nm.spawnPrefabs.Add(directorPrefab);

            for (int i = 0; i < 8; i++)
            {
                GameObject sp = new GameObject($"Start {i}");
                sp.transform.position = new Vector3(-7f + i * 2f, 1.1f, StartZ);
                sp.AddComponent<NetworkStartPosition>();
            }

            System.IO.Directory.CreateDirectory("Assets/_Party/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/_Party/Scenes/NetTest.unity", true),
                new EditorBuildSettingsScene(ScenePath, true),
            };

            Debug.Log("[Party] Red Light scene built.");
        }

        /// <summary>
        /// Obstacles along the lane so the run is a course, not a corridor.
        ///
        /// All primitives, no art. They are placed between the start and the finish with
        /// a clear gap at each end so nobody is shoved before they have moved.
        /// </summary>
        static void BuildHazards(float halfWidth, float startZ, float finishZ)
        {
            Material hazard = PresentationSetup.Lit("Hazard", new Color(0.88f, 0.32f, 0.30f), 0.35f,
                                                    0f, new Color(0.30f, 0.05f, 0.04f));
            Material pillar = PresentationSetup.Lit("Pillar", new Color(0.55f, 0.58f, 0.66f), 0.3f);

            GameObject root = new GameObject("Hazards");
            float first = startZ + 14f, last = finishZ - 10f;
            int slots = 7;

            for (int i = 0; i < slots; i++)
            {
                float z = Mathf.Lerp(first, last, i / (float)(slots - 1));

                if (i % 3 == 0)
                {
                    // Sweeping bar - forces a decision about when to commit.
                    GameObject pivot = new GameObject($"Sweeper {i}");
                    pivot.transform.SetParent(root.transform, false);
                    pivot.transform.localPosition = new Vector3(0f, 0.9f, z);
                    SweeperBar sb = pivot.AddComponent<SweeperBar>();
                    sb.degreesPerSecond = (i % 2 == 0 ? 1 : -1) * Random.Range(40f, 70f);
                    sb.phaseOffset = Random.Range(0f, 360f);

                    GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bar.transform.SetParent(pivot.transform, false);
                    bar.transform.localScale = new Vector3(halfWidth * 1.25f, 0.7f, 0.7f);
                    bar.GetComponent<Renderer>().sharedMaterial = hazard;

                    GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.DestroyImmediate(post.GetComponent<Collider>());
                    post.transform.SetParent(pivot.transform, false);
                    post.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
                    post.GetComponent<Renderer>().sharedMaterial = pillar;
                }
                else if (i % 3 == 1)
                {
                    // Pistons that only shove while GO is active.
                    for (int s2 = -1; s2 <= 1; s2 += 2)
                    {
                        GameObject blk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        blk.name = $"Piston {i}{(s2 < 0 ? "L" : "R")}";
                        blk.transform.SetParent(root.transform, false);
                        blk.transform.localPosition = new Vector3(s2 * (halfWidth - 2.5f), 0.8f, z);
                        blk.transform.localScale = new Vector3(2.6f, 1.6f, 1.4f);
                        blk.GetComponent<Renderer>().sharedMaterial = hazard;
                        PistonBlock pb = blk.AddComponent<PistonBlock>();
                        pb.travel = 3.2f;
                        pb.speed = Random.Range(1.0f, 1.8f);
                        pb.phaseOffset = Random.Range(0f, 6f) * (s2 < 0 ? 1f : -1f);
                    }
                }
                else
                {
                    // Static pillars - a slalom that punishes running blind.
                    for (int k = -1; k <= 1; k++)
                    {
                        if (k == 0) continue;
                        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        c.name = $"Pillar {i}{k}";
                        c.transform.SetParent(root.transform, false);
                        c.transform.localPosition = new Vector3(k * 3.2f, 1.1f, z);
                        c.transform.localScale = new Vector3(1.5f, 1.1f, 1.5f);
                        c.GetComponent<Renderer>().sharedMaterial = pillar;
                    }
                }
            }
        }

        static void MakeStripe(string name, float z, Color c)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.name = name;
            s.transform.position = new Vector3(0f, 0.02f, z);
            s.transform.localScale = new Vector3(16f, 0.04f, 0.5f);
            Object.DestroyImmediate(s.GetComponent<BoxCollider>());
            s.GetComponent<Renderer>().sharedMaterial =
                PresentationSetup.Lit(name, c, 0.5f, 0f, c * 0.35f);
        }
    }
}
