using kcp2k;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Party.Ragdoll;

namespace Party.EditorTools
{
    /// <summary>
    /// A test bed for the active ragdoll. Not a minigame, and deliberately not networked.
    ///
    /// Feel is tuned by running it and watching, and every extra system in the scene is
    /// another thing that can be blamed for the character feeling wrong. So: a floor, some
    /// crates to pick up, a few bots to shove, and nothing else. The netcode cost of eight
    /// bodies per player is real and gets measured separately - putting it in now would mean
    /// tuning muscle springs through a network jitter buffer.
    ///
    ///     Unity -batchmode -quit -executeMethod Party.EditorTools.RagdollSetup.Build
    /// </summary>
    public static class RagdollSetup
    {
        const string ScenePath = "Assets/_Party/Scenes/RagdollLab.unity";

        [MenuItem("Party/Rebuild Ragdoll lab")]
        public static void Build()
        {
            Random.InitState(20260903);
            // Persist the transparent/emissive materials so their shader variants survive
            // into the player - built at runtime they get stripped and silently fall back.
            PresentationSetup.RagdollMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Floor";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial =
                PresentationSetup.Lit("LabFloor", new Color(0.63f, 0.56f, 0.86f), 0.16f);

            GameObject lightGo = new GameObject("Directional Light");
            lightGo.AddComponent<Light>().type = LightType.Directional;
            PresentationSetup.Lighting(lightGo);
            PresentationSetup.GlobalVolume();

            bool soloEarly = System.Environment.GetEnvironmentVariable("RAGDOLL_SOLO") == "1";
            // Things to pick up and throw. Varied masses on purpose: a light crate flies and a
            // heavy one drags you round, and you should be able to feel which is which
            // through the grab alone.
            Material crateMat = PresentationSetup.Lit("Crate", new Color(1f, 0.71f, 0.30f), 0.3f);
            for (int i = 0; i < ((soloEarly || System.Environment.GetEnvironmentVariable("RAGDOLL_PROBE") == "1") ? 0 : 8); i++)
            {
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Crate {i}";
                float s = Random.Range(0.4f, 0.85f);
                box.transform.localScale = Vector3.one * s;
                box.transform.position = new Vector3(Random.Range(-6f, 6f), 0.6f, Random.Range(-4f, 6f));
                box.GetComponent<Renderer>().sharedMaterial = crateMat;
                var rb = box.AddComponent<Rigidbody>();
                rb.mass = s * 6f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            // The player, plus three bots to have something to grab that grabs back.
            // RAGDOLL_PROBE=1 builds the VERIFICATION scene: one character, one crate at a
            // known place, and a script that walks it over, grabs, carries, throws and goes
            // limp - each step measured. Grab and throw cannot be exercised any other way
            // headlessly, so without this they ship untested.
            bool probe = System.Environment.GetEnvironmentVariable("RAGDOLL_PROBE") == "1";
            if (probe)
            {
                GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = "ProbeCrate";
                crate.transform.localScale = Vector3.one * 0.5f;
                crate.transform.position = new Vector3(0f, 0.35f, 1.5f);
                crate.GetComponent<Renderer>().sharedMaterial =
                    PresentationSetup.Lit("Crate", new Color(1f, 0.71f, 0.30f), 0.3f);
                var crb = crate.AddComponent<Rigidbody>();
                crb.mass = 1.2f;
                crb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                GameObject actor = new GameObject("Probe");
                actor.transform.position = new Vector3(0f, 0f, -2.5f);
                actor.AddComponent<RagdollMuscles>();
                var dr = actor.AddComponent<RagdollDriver>();
                dr.livery = new Color(0.92f, 0.28f, 0.45f);
                dr.bot = false;               // the probe drives it, not the bot policy
                dr.probeDriven = true;
                var pr = actor.AddComponent<RagdollProbe>();
                pr.target = crb;
            }

            // RAGDOLL_SOLO=1 builds one still character on an empty floor. Tuning a balance
            // controller with four bots barging each other over crates means every wobble has
            // four possible causes; this is the same "change one thing" discipline the build
            // investigation needed (HANDOFF §6.8).
            bool solo = soloEarly;
            // In solo mode the lone character is a BOT, so it walks - a controller that only
            // holds a still pose is not evidence of anything.
            if (!probe) MakeActor("Player", new Vector3(0f, 0f, -3f), new Color(0.92f, 0.28f, 0.45f), solo);
            if (!solo && !probe)
            {
                MakeActor("Bot A", new Vector3(-2.5f, 0f, 1f), new Color(0.36f, 0.88f, 0.90f), true);
                MakeActor("Bot B", new Vector3( 2.5f, 0f, 1f), new Color(1f, 0.82f, 0.34f), true);
                MakeActor("Bot C", new Vector3( 0f,   0f, 3.5f), new Color(0.66f, 0.55f, 1f), true);
            }

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 55f;
            camGo.transform.position = new Vector3(0f, 4.2f, -9.5f);
            camGo.transform.rotation = Quaternion.Euler(16f, 0f, 0f);
            camGo.AddComponent<AudioListener>();

            var cd = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (cd == null) cd = camGo.AddComponent<UniversalAdditionalCameraData>();
            cd.renderPostProcessing = true;
            cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            camGo.AddComponent<RagdollLabHUD>();
            camGo.AddComponent<LabAutoShot>();

            System.IO.Directory.CreateDirectory("Assets/_Party/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == ScenePath))
                list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();

            Debug.Log("[Party] Ragdoll lab built.");
        }

        static void MakeActor(string name, Vector3 pos, Color livery, bool bot)
        {
            GameObject go = new GameObject(name);
            go.transform.position = pos;
            go.AddComponent<RagdollMuscles>();
            var d = go.AddComponent<RagdollDriver>();
            d.livery = livery;
            d.bot = bot;
        }
    }
}
