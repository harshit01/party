using kcp2k;
using Party.Juice;
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

        /// <summary>
        /// Body + face, built from primitives.
        ///
        /// The VISUAL is a child of the networked root. NetworkTransform replicates the
        /// root; squash, stretch and lean happen on the child, so the juice can never
        /// fight the netcode.
        /// </summary>
        static (Transform visual, Transform face, Renderer body) BuildBody(GameObject root)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(visual.transform, false);

            // A face gives a capsule a front, and a front is most of what makes it a
            // character rather than a marker.
            GameObject face = new GameObject("Face");
            face.transform.SetParent(visual.transform, false);
            face.transform.localPosition = new Vector3(0f, 0.42f, 0f);

            Material white = PresentationSetup.Lit("EyeWhite", Color.white, 0.55f);
            Material pupil = PresentationSetup.Lit("EyePupil", new Color(0.06f, 0.06f, 0.09f), 0.7f);

            for (int s = -1; s <= 1; s += 2)
            {
                GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = s < 0 ? "EyeL" : "EyeR";
                Object.DestroyImmediate(eye.GetComponent<Collider>());
                eye.transform.SetParent(face.transform, false);
                eye.transform.localPosition = new Vector3(s * 0.17f, 0f, 0.36f);
                eye.transform.localScale = Vector3.one * 0.26f;
                eye.GetComponent<Renderer>().sharedMaterial = white;

                GameObject p2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p2.name = "Pupil";
                Object.DestroyImmediate(p2.GetComponent<Collider>());
                p2.transform.SetParent(eye.transform, false);
                p2.transform.localPosition = new Vector3(0f, 0f, 0.34f);
                p2.transform.localScale = Vector3.one * 0.55f;
                p2.GetComponent<Renderer>().sharedMaterial = pupil;
            }

            return (visual.transform, face.transform, body.GetComponent<Renderer>());
        }

        static GameObject BuildPlayerPrefab()
        {
            GameObject root = new GameObject("PartyPlayer");

            var (visual, face, bodyRend) = BuildBody(root);

            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.height = 2f; col.radius = 0.5f;

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 1f;
            // Damping matters more than it looks: without it a capsule coasts for about
            // a metre after you release the keys, so EVERY freeze registers as movement
            // and the first STOP eliminates the entire lobby. Physics still does the
            // acting - it just does not skate.
            rb.linearDamping = 4.5f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            root.AddComponent<NetworkIdentity>();

            // Host-authoritative: the server owns the transform, clients receive it.
            NetworkTransformReliable nt = root.AddComponent<NetworkTransformReliable>();
            nt.syncDirection = SyncDirection.ServerToClient;
            nt.syncRotation  = false;   // capsules do not rotate; saves bandwidth
            nt.syncScale     = false;

            // Name label - parented to the ROOT, not the visual, so squash and lean
            // never wobble the text.
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
            so.FindProperty("bodyRenderer").objectReferenceValue = bodyRend;
            so.FindProperty("nameTag").objectReferenceValue = tm;
            so.ApplyModifiedPropertiesWithoutUndo();

            PlayerJuice juice = root.AddComponent<PlayerJuice>();
            juice.visual = visual;
            juice.face   = face;

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
