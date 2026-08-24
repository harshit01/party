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
            d.startZ = -12f;
            d.finishZ = 14f;

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

            const float HalfWidth = 8f;

            // A long lane rather than an open plane: Red Light needs a direction to run in.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Lane";
            ground.transform.localScale = new Vector3(1.6f, 1f, 3.4f);
            ground.GetComponent<Renderer>().sharedMaterial =
                PresentationSetup.Lit("Lane", new Color(0.30f, 0.33f, 0.40f), 0.18f);

            MakeStripe("StartLine",  -12f, new Color(0.90f, 0.92f, 0.96f));
            MakeStripe("FinishLine",  14f, new Color(0.98f, 0.80f, 0.25f));

            PresentationSetup.Barriers(HalfWidth, -20f, 20f);
            PresentationSetup.FinishGantry(14f, HalfWidth);
            PresentationSetup.Crowd(HalfWidth, -18f, 18f);

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
            camGo.transform.position = new Vector3(0f, 12f, -26f);
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
                sp.transform.position = new Vector3(-7f + i * 2f, 1.1f, -12f);
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
