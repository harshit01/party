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

            BuildArena();

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

        /// <summary>
        /// The arena, and NOTHING IN IT IS A BARE BOX.
        ///
        /// The first version was one cube and two cylinders. Animated characters on static
        /// geometry read worse than either alone - the stillness of the set draws attention
        /// to itself. Fall Guys and Gang Beasts arenas are never still, and almost none of
        /// that motion is expensive: it is sine waves on transforms, driven from NetworkTime
        /// so every machine draws the hammer in the same place without syncing it.
        /// </summary>
        static void BuildArena()
        {
            Material deck   = PresentationSetup.Lit("Deck", new Color(1f, 0.71f, 0.30f), 0.25f);
            Material trim   = PresentationSetup.Lit("Trim", new Color(1f, 0.45f, 0.62f), 0.3f);
            Material post   = PresentationSetup.Lit("Post", new Color(0.63f, 0.56f, 0.86f), 0.2f);
            Material hammer = PresentationSetup.Lit("Hammer", new Color(0.36f, 0.88f, 0.90f), 0.35f);

            GameObject arena = new GameObject("Arena");

            // SEGMENTED, AND EACH SEGMENT TILTS UNDER WEIGHT. This is the piece that stops
            // the arena being furniture: where you stand matters, and two people crowding
            // one end is a mistake you can watch developing.
            const int Segments = 7;
            for (int i = 0; i < Segments; i++)
            {
                float z = Mathf.Lerp(-HalfLength + 2f, HalfLength - 2f, i / (float)(Segments - 1));
                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"Deck {i}";
                seg.transform.SetParent(arena.transform, false);
                seg.transform.position = new Vector3(0f, 0.5f, z);
                seg.transform.localScale = new Vector3(2.3f, 0.45f, (HalfLength * 2f) / Segments - 0.15f);
                seg.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? deck : trim;

                var rb = seg.AddComponent<Rigidbody>();
                rb.isKinematic = true;      // moves the world, is not moved by it
                var t = seg.AddComponent<Party.Arena.Tilter>();
                t.maxTilt = 7f;
                t.halfWidth = 1.15f;

                // Rounded caps, so the deck reads as inflatable rather than as a plank of
                // MDF. Chunky and soft is the entire visual language of the reference.
                for (int e = -1; e <= 1; e += 2)
                {
                    GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.DestroyImmediate(cap.GetComponent<Collider>());
                    cap.transform.SetParent(seg.transform, false);
                    cap.transform.localPosition = new Vector3(e * 0.5f, 0f, 0f);
                    cap.transform.localRotation = Quaternion.Euler(90f, 0f, 90f);
                    cap.transform.localScale = new Vector3(1f, 0.5f, 1f);
                    cap.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? deck : trim;
                }
            }

            // SWINGING HAMMERS. Real colliders on a kinematic pendulum, so they genuinely
            // sweep people off - the arena participates rather than watching.
            for (int i = 0; i < 2; i++)
            {
                float z = i == 0 ? -5.5f : 6.5f;
                GameObject pivot = new GameObject($"Hammer {i}");
                pivot.transform.SetParent(arena.transform, false);
                pivot.transform.position = new Vector3(0f, 6.2f, z);
                var sw = pivot.AddComponent<Party.Arena.Swinger>();
                sw.degrees = 62f;
                sw.period = 3.4f + i * 0.7f;
                sw.phase = i * 1.9f;
                sw.axis = Vector3.forward;

                GameObject rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(rope.GetComponent<Collider>());
                rope.transform.SetParent(pivot.transform, false);
                rope.transform.localPosition = new Vector3(0f, -2.3f, 0f);
                rope.transform.localScale = new Vector3(0.12f, 2.3f, 0.12f);
                rope.GetComponent<Renderer>().sharedMaterial = post;

                GameObject head = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                head.transform.SetParent(pivot.transform, false);
                head.transform.localPosition = new Vector3(0f, -4.7f, 0f);
                head.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                head.transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);
                head.GetComponent<Renderer>().sharedMaterial = hammer;
                var hrb = head.AddComponent<Rigidbody>();
                hrb.isKinematic = true;
            }

            // A sweeping beam at the centre - low, so it takes the legs rather than the head.
            GameObject spinPivot = new GameObject("Sweeper");
            spinPivot.transform.SetParent(arena.transform, false);
            spinPivot.transform.position = new Vector3(0f, 1.15f, 0.5f);
            var sp = spinPivot.AddComponent<Party.Arena.Spinner>();
            sp.degreesPerSecond = 38f;

            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            beam.transform.SetParent(spinPivot.transform, false);
            beam.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            beam.transform.localScale = new Vector3(0.34f, 3.2f, 0.34f);
            beam.GetComponent<Renderer>().sharedMaterial = hammer;
            var brb = beam.AddComponent<Rigidbody>();
            brb.isKinematic = true;

            // End posts, bobbing gently so even the scenery is breathing.
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject p2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                p2.name = s < 0 ? "Post L" : "Post R";
                p2.transform.SetParent(arena.transform, false);
                p2.transform.position = new Vector3(0f, -2f, s * (HalfLength + 0.6f));
                p2.transform.localScale = new Vector3(1.8f, 2.6f, 1.8f);
                p2.GetComponent<Renderer>().sharedMaterial = post;
            }

            // BISECT TOGGLES. The arena made the Plank scene corrupt on 12 of 12 builds -
            // deterministic, not the usual lottery - so one of these pieces is the culprit
            // and guessing which would be the cumulative-isolation mistake all over again
            // (HANDOFF §6.8). Set ARENA_NO_<PART>=1 to drop one at a time.
            if (System.Environment.GetEnvironmentVariable("ARENA_NO_BUNTING") != "1")
                BuildBunting(arena.transform, trim);
            if (System.Environment.GetEnvironmentVariable("ARENA_NO_CROWD") != "1")
                BuildCrowd(arena.transform);
            if (System.Environment.GetEnvironmentVariable("ARENA_NO_BALLOONS") != "1")
                BuildBalloons(arena.transform);
        }

        static void BuildBunting(Transform parent, Material m)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject row = new GameObject($"Bunting {(side < 0 ? "L" : "R")}");
                row.transform.SetParent(parent, false);
                row.transform.position = new Vector3(side * 5.5f, 5.4f, 0f);
                row.AddComponent<Party.Arena.Bunting>();

                for (int i = 0; i < 18; i++)
                {
                    GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(flag.GetComponent<Collider>());
                    flag.transform.SetParent(row.transform, false);
                    // x is used as the phase axis by Bunting, so lay the row out along x and
                    // rotate the whole row into place.
                    flag.transform.localPosition = new Vector3(-14f + i * 1.7f, 0f, 0f);
                    flag.transform.localScale = new Vector3(0.4f, 0.5f, 0.06f);
                    flag.GetComponent<Renderer>().sharedMaterial = m;
                }
                row.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }
        }

        static void BuildCrowd(Transform parent)
        {
            GameObject ring = new GameObject("Crowd");
            ring.transform.SetParent(parent, false);
            ring.AddComponent<Party.Arena.CrowdRing>();

            Material[] cols =
            {
                PresentationSetup.Lit("Fan0", new Color(0.62f, 0.48f, 0.66f), 0.2f),
                PresentationSetup.Lit("Fan1", new Color(0.46f, 0.52f, 0.76f), 0.2f),
                PresentationSetup.Lit("Fan2", new Color(0.68f, 0.62f, 0.58f), 0.2f),
            };

            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 3; row++)
                    for (int i = 0; i < 22; i++)
                    {
                        GameObject fan = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        Object.DestroyImmediate(fan.GetComponent<Collider>());
                        fan.transform.SetParent(ring.transform, false);
                        fan.transform.localPosition = new Vector3(
                            side * (7f + row * 1.9f),
                            -1.2f + row * 0.8f,
                            -HalfLength + i * (HalfLength * 2f / 21f));
                        fan.transform.localScale = Vector3.one * 0.62f;
                        fan.GetComponent<Renderer>().sharedMaterial = cols[(i + row) % cols.Length];
                    }
        }

        static void BuildBalloons(Transform parent)
        {
            Material[] cols =
            {
                PresentationSetup.Lit("Bal0", new Color(1f, 0.82f, 0.34f), 0.6f),
                PresentationSetup.Lit("Bal1", new Color(0.36f, 0.88f, 0.90f), 0.6f),
                PresentationSetup.Lit("Bal2", new Color(1f, 0.45f, 0.62f), 0.6f),
            };

            for (int i = 0; i < 10; i++)
            {
                GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(b.GetComponent<Collider>());
                b.transform.SetParent(parent, false);
                b.transform.position = new Vector3(
                    (i % 2 == 0 ? -1f : 1f) * Random.Range(6f, 11f),
                    Random.Range(5f, 9f),
                    Random.Range(-HalfLength, HalfLength));
                b.transform.localScale = Vector3.one * Random.Range(0.8f, 1.4f);
                b.GetComponent<Renderer>().sharedMaterial = cols[i % cols.Length];

                var bob = b.AddComponent<Party.Arena.Bobber>();
                bob.height = Random.Range(0.3f, 0.7f);
                bob.period = Random.Range(3f, 6f);
                bob.phase = Random.Range(0f, 6.28f);
            }
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
