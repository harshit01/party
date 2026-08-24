using kcp2k;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Party.EditorTools
{
    /// <summary>
    /// Builds the netcode-milestone scene and player prefab from code.
    ///
    /// Done as a script rather than by hand in the editor so the setup is reproducible
    /// and reviewable in a diff - and so the Windows machine gets an identical scene
    /// instead of one assembled from a screenshot.
    ///
    ///     Unity -batchmode -quit -executeMethod Party.EditorTools.MilestoneSetup.Build
    /// </summary>
    public static class MilestoneSetup
    {
        const string PrefabPath = "Assets/_Party/Prefabs/PartyPlayer.prefab";
        const string ScenePath  = "Assets/_Party/Scenes/NetTest.unity";

        [MenuItem("Party/Rebuild milestone scene")]
        public static void Build()
        {
            GameObject prefab = BuildPlayerPrefab();
            BuildScene(prefab);
            Debug.Log("[Party] milestone scene + prefab built.");
        }

        static Font DefaultFont()
        {
            // Arial.ttf was removed in newer Unity; LegacyRuntime.ttf replaced it.
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        static GameObject BuildPlayerPrefab()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "PartyPlayer";
            Object.DestroyImmediate(root.GetComponent<CapsuleCollider>());

            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.height = 2f; col.radius = 0.5f;

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            root.AddComponent<NetworkIdentity>();

            // Host-authoritative: the server owns the transform, clients receive it.
            NetworkTransformReliable nt = root.AddComponent<NetworkTransformReliable>();
            nt.syncDirection = SyncDirection.ServerToClient;
            nt.syncRotation  = false;   // capsules do not rotate; saves bandwidth
            nt.syncScale     = false;

            // Name label
            GameObject tag = new GameObject("NameTag");
            tag.transform.SetParent(root.transform, false);
            tag.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            TextMesh tm = tag.AddComponent<TextMesh>();
            tm.text = "?";
            tm.font = DefaultFont();
            tm.characterSize = 0.16f;
            tm.fontSize = 72;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            MeshRenderer tr = tag.GetComponent<MeshRenderer>();
            if (tm.font != null) tr.sharedMaterial = tm.font.material;

            PartyPlayer pp = root.AddComponent<PartyPlayer>();
            SerializedObject so = new SerializedObject(pp);
            so.FindProperty("bodyRenderer").objectReferenceValue = root.GetComponent<MeshRenderer>();
            so.FindProperty("nameTag").objectReferenceValue = tm;
            so.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory("Assets/_Party/Prefabs");
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return asset;
        }

        static void BuildScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ground
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(3f, 1f, 3f);   // 30 x 30 m

            // Light
            GameObject lightGo = new GameObject("Directional Light");
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            l.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Camera looking down at the plane
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.transform.position = new Vector3(0f, 18f, -16f);
            camGo.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            camGo.AddComponent<AudioListener>();

            // Networking
            GameObject netGo = new GameObject("NetworkManager");

            // Both transports live on the object; PartyNetworkManager.Awake picks one.
            // Steam when it is genuinely up, direct-IP otherwise - so every local and LAN
            // test still runs on a machine with no Steam.
            KcpTransport kcp = netGo.AddComponent<KcpTransport>();
            Mirror.FizzySteam.FizzySteamworks fizzy = netGo.AddComponent<Mirror.FizzySteam.FizzySteamworks>();

            netGo.AddComponent<SteamBoot>();

            PartyNetworkManager nm = netGo.AddComponent<PartyNetworkManager>();
            nm.transport          = kcp;      // default; overridden in Awake
            nm.localTransport     = kcp;
            nm.steamTransport     = fizzy;
            nm.playerPrefab       = playerPrefab;
            nm.targetParticipants = 4;
            nm.headlessStartMode  = HeadlessStartOptions.DoNothing;  // driven by MilestoneAutoRun instead

            netGo.AddComponent<SteamLobby>();
            netGo.AddComponent<PartyHUD>();
            netGo.AddComponent<MilestoneAutoRun>();

            // Spawn points spread around the middle
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                GameObject sp = new GameObject($"Start {i}");
                sp.transform.position = new Vector3(Mathf.Cos(a) * 5f, 1.1f, Mathf.Sin(a) * 5f);
                sp.AddComponent<NetworkStartPosition>();
            }

            System.IO.Directory.CreateDirectory("Assets/_Party/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
