using kcp2k;
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
        const string ScenePath  = "Assets/_Party/Scenes/RedLight.unity";
        const string PrefabPath = "Assets/_Party/Prefabs/PartyPlayer.prefab";

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

            // A long lane rather than an open plane: Red Light needs a direction to run in.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Lane";
            ground.transform.localScale = new Vector3(1.6f, 1f, 3.4f);

            MakeStripe("StartLine",  -12f, new Color(0.85f, 0.85f, 0.85f));
            MakeStripe("FinishLine",  14f, new Color(0.95f, 0.82f, 0.2f));

            GameObject lightGo = new GameObject("Directional Light");
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.1f; l.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Camera behind the start line looking up the lane - players see the finish,
            // and Barnaby's banner sits above it.
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.transform.position = new Vector3(0f, 12f, -26f);
            camGo.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            camGo.AddComponent<AudioListener>();

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
            netGo.AddComponent<HostVoice>();

            RedLightDirector dir = netGo.AddComponent<RedLightDirector>();
            dir.startZ = -12f;
            dir.finishZ = 14f;

            netGo.AddComponent<RedLightHUD>();
            netGo.AddComponent<MilestoneAutoRun>();

            // The director is a NetworkBehaviour, so it needs an identity to replicate.
            netGo.AddComponent<NetworkIdentity>();

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
            s.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = c };
        }
    }
}
