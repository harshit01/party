using kcp2k;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Party;
using Party.Plank;
using Party.Ragdoll;
using Party.Juice;

namespace Party.EditorTools
{
    /// <summary>
    /// Builds "Plank Panic" (MINIGAMES.md #1, Family A) - the first minigame that needs the
    /// active ragdoll to exist at all.
    ///
    /// The arena is deliberately plain: a beam over nothing. Family A's premise is "physics
    /// character + platform + shove", and everything interesting here is supposed to come
    /// out of bodies colliding on a narrow surface rather than out of set dressing. The
    /// dressed version goes to Docs/ArtTarget/ for approval like every other visual, and the
    /// mechanic is fully testable without it.
    ///
    ///     Unity -batchmode -quit -executeMethod Party.EditorTools.PlankSetup.Build
    /// </summary>
    public static class PlankSetup
    {
        const string ScenePath    = "Assets/_Party/Scenes/Plank.unity";
        const string PrefabPath   = "Assets/_Party/Prefabs/PartyPlayer.prefab";
        const string DirectorPath = "Assets/_Party/Prefabs/PlankDirector.prefab";
        const float  HalfLength   = 14f;

        [MenuItem("Party/Rebuild Plank scene")]
        public static void Build()
        {
            Random.InitState(20260903);
            PresentationSetup.RagdollMaterials();

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError("[Party] player prefab missing - run Party/Rebuild milestone scene first.");
                EditorApplication.Exit(1);
                return;
            }
            if (playerPrefab.GetComponent<RagdollSync>() == null)
            {
                playerPrefab.AddComponent<RagdollSync>();
                EditorUtility.SetDirty(playerPrefab);
                AssetDatabase.SaveAssets();
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // THE PLANK. Narrow enough that standing still is not free - a wide beam turns
            // the game into a shoving match with no jeopardy, and the jeopardy is the point.
            GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = "Plank";
            plank.transform.position = new Vector3(0f, 0.5f, 0f);
            plank.transform.localScale = new Vector3(2.2f, 0.5f, HalfLength * 2f);
            plank.GetComponent<Renderer>().sharedMaterial =
                PresentationSetup.Lit("Plank", new Color(1f, 0.71f, 0.30f), 0.25f);

            // Posts at each end, so the drop reads as a drop rather than as the world ending.
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = s < 0 ? "Post L" : "Post R";
                post.transform.position = new Vector3(0f, -2f, s * HalfLength);
                post.transform.localScale = new Vector3(1.6f, 2.5f, 1.6f);
                post.GetComponent<Renderer>().sharedMaterial =
                    PresentationSetup.Lit("Post", new Color(0.63f, 0.56f, 0.86f), 0.2f);
            }

            GameObject lightGo = new GameObject("Directional Light");
            lightGo.AddComponent<Light>().type = LightType.Directional;
            PresentationSetup.Lighting(lightGo);
            PresentationSetup.GlobalVolume();

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 55f;
            camGo.transform.position = new Vector3(0f, 6f, -20f);
            camGo.transform.rotation = Quaternion.Euler(14f, 0f, 0f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraRig>();

            var cd = camGo.GetComponent<UniversalAdditionalCameraData>();
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
            nm.targetParticipants = 5;
            nm.spawnRagdolls = true;          // the entire reason this game can exist
            nm.headlessStartMode = HeadlessStartOptions.DoNothing;

            netGo.AddComponent<SteamLobby>();
            netGo.AddComponent<PartyHUD>();
            netGo.AddComponent<PlankHUD>();
            netGo.AddComponent<MilestoneAutoRun>();
            netGo.AddComponent<NetTrafficProbe>();

            GameObject directorPrefab = BuildDirectorPrefab();
            nm.serverSpawnOnStart = new[] { directorPrefab };
            nm.spawnPrefabs.Add(directorPrefab);

            for (int i = 0; i < 8; i++)
            {
                GameObject sp = new GameObject($"Start {i}");
                sp.transform.position = new Vector3(0f, 1.4f, -HalfLength + 2f + i * 3.4f);
                sp.AddComponent<NetworkStartPosition>();
            }

            System.IO.Directory.CreateDirectory("Assets/_Party/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(x => x.path == ScenePath))
                list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();

            Debug.Log("[Party] Plank scene built (plain arena - dressing pending approval).");
        }

        static GameObject BuildDirectorPrefab()
        {
            System.IO.Directory.CreateDirectory("Assets/_Party/Prefabs");
            GameObject go = new GameObject("PlankDirector");
            go.AddComponent<NetworkIdentity>();
            var voice = go.AddComponent<Party.RedLight.HostVoice>();
            voice.gameName = "Plank Panic";
            var d = go.AddComponent<PlankDirector>();
            d.plankHalfLength = HalfLength;

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, DirectorPath);
            Object.DestroyImmediate(go);
            return asset;
        }
    }
}
