using kcp2k;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Mirror;
using Party;
using Party.SayWhat;
using Party.Juice;

namespace Party.EditorTools
{
    /// <summary>
    /// Builds the "Say What He Says" scene (MINIGAMES.md #10) from code.
    ///
    /// DELIBERATELY A PLACEHOLDER STAGE. The founder's standing rule is that anything
    /// visual gets a reference image agreed FIRST - three passes were once spent guessing
    /// at a look that could have been shown in one screenshot (HANDOFF.md §6.9), and it
    /// has already paid for itself twice. So this builds a flat, honest stage that is good
    /// enough to test the MECHANIC headlessly, and no more: no set dressing, no crowd, no
    /// composed camera angle. The arena design goes to Docs/ArtTarget/ for approval and
    /// gets built afterwards.
    ///
    /// What it does share with Red Light is the toolkit - the same player prefab, network
    /// manager, bias, host voice and follow camera - which is the entire argument for
    /// building minigames in families (MINIGAMES.md: "game #4 in a family costs a fraction
    /// of game #1").
    ///
    ///     Unity -batchmode -quit -executeMethod Party.EditorTools.SayWhatSetup.Build
    /// </summary>
    public static class SayWhatSetup
    {
        const string ScenePath    = "Assets/_Party/Scenes/SayWhat.unity";
        const string PrefabPath   = "Assets/_Party/Prefabs/PartyPlayer.prefab";
        const string DirectorPath = "Assets/_Party/Prefabs/SayWhatDirector.prefab";

        [MenuItem("Party/Rebuild Say What scene")]
        public static void Build()
        {
            // Same fixed seed discipline as Red Light. Note this does NOT make the saved
            // scene byte-identical between runs - Unity assigns random anchor ids and
            // writes objects in a varying order - which is why the verified scene is
            // committed rather than regenerated per build. See HANDOFF KNOWN ISSUE.
            Random.InitState(20260901);

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError("[Party] player prefab missing - run Party/Rebuild milestone scene first.");
                EditorApplication.Exit(1);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // A plain stage. Players stand on it and perform; there is nowhere to run to,
            // which is the point - this game is memory, not movement.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Stage";
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            ground.GetComponent<Renderer>().sharedMaterial =
                PresentationSetup.Lit("Stage", new Color(0.63f, 0.56f, 0.86f), 0.16f);

            GameObject lightGo = new GameObject("Directional Light");
            lightGo.AddComponent<Light>().type = LightType.Directional;
            PresentationSetup.Lighting(lightGo);
            PresentationSetup.GlobalVolume();

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 52f;
            camGo.transform.position = new Vector3(0f, 3.1f, -7f);
            camGo.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraRig>();

            UniversalAdditionalCameraData cd = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (cd == null) cd = camGo.AddComponent<UniversalAdditionalCameraData>();
            cd.renderPostProcessing = true;
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
            netGo.AddComponent<SayWhatHUD>();
            netGo.AddComponent<MilestoneAutoRun>();

            GameObject directorPrefab = BuildDirectorPrefab();
            nm.serverSpawnOnStart = new[] { directorPrefab };
            nm.spawnPrefabs.Add(directorPrefab);

            // A ring, so everyone is visible to everyone. In a game about watching people
            // get it wrong, nobody should be behind you.
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                GameObject sp = new GameObject($"Start {i}");
                sp.transform.position = new Vector3(Mathf.Sin(a) * 4.5f, 1.1f, Mathf.Cos(a) * 4.5f - 1f);
                sp.AddComponent<NetworkStartPosition>();
            }

            System.IO.Directory.CreateDirectory("Assets/_Party/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/_Party/Scenes/NetTest.unity", true),
                new EditorBuildSettingsScene("Assets/_Party/Scenes/RedLight.unity", true),
                new EditorBuildSettingsScene(ScenePath, true),
            };

            Debug.Log("[Party] Say What scene built (placeholder stage - arena pending approval).");
        }

        /// <summary>The director as a server-spawned prefab, exactly like Red Light's.</summary>
        static GameObject BuildDirectorPrefab()
        {
            System.IO.Directory.CreateDirectory("Assets/_Party/Prefabs");

            GameObject go = new GameObject("SayWhatDirector");
            go.AddComponent<NetworkIdentity>();
            // The SAME HostVoice component Red Light uses, told which game it is
            // narrating. Barnaby's memory across the night is the product; a separate
            // voice per minigame would make him a different character each round.
            var voice = go.AddComponent<Party.RedLight.HostVoice>();
            voice.gameName = "Say What He Says";
            go.AddComponent<SayWhatDirector>();

            // #11 "The Prediction" wraps #10 here. It is not a separate scene because it
            // is not a separate game - it bets on whichever minigame is present, which is
            // the seam the round loop will use. Betting first, then Say What, then the
            // reveal.
            go.AddComponent<Party.Prediction.PredictionDirector>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, DirectorPath);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
