using Party.Juice;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Party.EditorTools
{
    /// <summary>
    /// Everything that makes it read as a game, built from code and primitives only.
    ///
    /// ZERO BESPOKE ART. No models, no textures, no purchases. This respects the
    /// documented discipline ("no bespoke art until the minigames have survived
    /// playtesting", and no art at all before the trademark/name check) while closing the
    /// gap between "capsules on a grey plane" and something that looks intentional.
    ///
    /// What actually does the work here is not geometry, it is LIGHTING and POST-
    /// PROCESSING. Untouched Unity defaults are what make a prototype look unfinished -
    /// flat ambient, no tonemapping, no bloom, a grey sky.
    /// </summary>
    public static class PresentationSetup
    {
        public const string MatDir     = "Assets/_Party/Art";
        public const string ProfilePath = MatDir + "/PartyPostFX.asset";
        public const string SkyPath     = MatDir + "/PartySky.mat";

        public static Material Lit(string name, Color c, float smoothness = 0.35f, float metallic = 0f,
                                   Color? emission = null)
        {
            System.IO.Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = c;
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Gradient sky. A flat grey background is most of why a scene looks dead.</summary>
        public static Material Sky()
        {
            System.IO.Directory.CreateDirectory(MatDir);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (m == null)
            {
                Shader sh = Shader.Find("Skybox/Procedural");
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, SkyPath);
            }
            // Bright, candy-coloured, high key - the Fall Guys reference in
            // Docs/ArtTarget/redlight_target.svg. The old sky was a moody navy that made
            // a party game show look like a car park at dusk.
            m.SetFloat("_SunSize", 0.06f);
            m.SetFloat("_AtmosphereThickness", 0.42f);
            m.SetColor("_SkyTint", new Color(0.44f, 0.76f, 0.95f));
            m.SetColor("_GroundColor", new Color(0.74f, 0.68f, 0.86f));
            m.SetFloat("_Exposure", 1.5f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Tonemapping, bloom, colour grading, vignette. This single asset is responsible
        /// for more of the "looks like a game" jump than any amount of geometry.
        /// </summary>
        public static VolumeProfile PostFX()
        {
            System.IO.Directory.CreateDirectory(MatDir);
            // RECREATE, never mutate.
            //
            // The previous version removed each VolumeComponent and DestroyImmediate'd the
            // sub-asset on every run. The profile kept the now-null entries, so the asset
            // ended up listing 8 components that were all {fileID: 0}. The editor tolerated
            // it; the BUILD did not - deserialising those nulls read past the end of the
            // scene data and the player died on startup with "level0 is corrupted", having
            // reported a successful build. Deleting and rebuilding the asset avoids the
            // whole class of problem.
            // VolumeProfile.Add<T>() creates the component IN MEMORY ONLY. Persisting it
            // requires AssetDatabase.AddObjectToAsset - without that every component is
            // written as {fileID: 0}, the profile lists 8 null entries, and the BUILD dies
            // deserialising them ("level0 is corrupted") while the editor shrugs it off.
            AssetDatabase.DeleteAsset(ProfilePath);
            VolumeProfile p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, ProfilePath);

            T Persist<T>() where T : VolumeComponent
            {
                T c = p.Add<T>(true);
                c.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, p);
                return c;
            }

            Tonemapping tm = Persist<Tonemapping>();
            tm.mode.overrideState = true; tm.mode.value = TonemappingMode.ACES;

            Bloom bl = Persist<Bloom>();
            bl.intensity.overrideState = true; bl.intensity.value = 1.15f;
            bl.threshold.overrideState = true; bl.threshold.value = 1.05f;
            bl.scatter.overrideState = true;   bl.scatter.value = 0.62f;

            ColorAdjustments ca = Persist<ColorAdjustments>();
            ca.postExposure.overrideState = true; ca.postExposure.value = 0.25f;
            ca.contrast.overrideState = true;     ca.contrast.value = 12f;
            ca.saturation.overrideState = true;   ca.saturation.value = 8f;   // restrained

            // DEPTH OF FIELD is the biggest single step from "prototype" to "produced":
            // a sharp subject against a softly blurred background reads as a photograph
            // rather than a flat render, and it hides geometry simplicity for free.
            // Depth of field stays MENU-ONLY in spirit but lives in the shared profile.
            // A focus distance tuned for a character 9m from the menu camera is wrong for
            // a gameplay camera much further back; if capsules render soft, gameplay wants
            // its own profile rather than a second set of guessed numbers here.
            DepthOfField dof = Persist<DepthOfField>();
            dof.mode.overrideState = true;          dof.mode.value = DepthOfFieldMode.Bokeh;
            dof.focusDistance.overrideState = true; dof.focusDistance.value = 9.2f;
            dof.aperture.overrideState = true;      dof.aperture.value = 8f;
            dof.focalLength.overrideState = true;   dof.focalLength.value = 62f;

            Vignette vg = Persist<Vignette>();
            vg.intensity.overrideState = true; vg.intensity.value = 0.32f;
            vg.smoothness.overrideState = true; vg.smoothness.value = 0.45f;

            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        /// <summary>Warm key light plus cool ambient - depth without any art.</summary>
        public static void Lighting(GameObject lightGo)
        {
            Light l = lightGo.GetComponent<Light>();
            l.color = new Color(1f, 0.97f, 0.92f);
            l.intensity = 1.35f;
            l.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            RenderSettings.skybox = Sky();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Bright ambient with a lifted ground bounce: the reference has almost no
            // black anywhere, and deep shadow is what made the old frame read as grim.
            RenderSettings.ambientSkyColor    = new Color(0.66f, 0.80f, 0.94f);
            RenderSettings.ambientEquatorColor= new Color(0.62f, 0.58f, 0.76f);
            RenderSettings.ambientGroundColor = new Color(0.46f, 0.40f, 0.56f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            // Fog tinted to the sky, so the far end of the lane dissolves into it rather
            // than ending in a hard grey band.
            RenderSettings.fogColor = new Color(0.78f, 0.84f, 0.94f);
            RenderSettings.fogStartDistance = 62f;
            RenderSettings.fogEndDistance = 165f;
        }

        public static GameObject GlobalVolume()
        {
            GameObject go = new GameObject("Global Volume");
            Volume v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.profile = PostFX();
            return go;
        }

        /// <summary>Two banks of bobbing primitives either side of the lane.</summary>
        public static void Crowd(float laneHalfWidth, float fromZ, float toZ)
        {
            // DESATURATED ON PURPOSE. These used to be fully saturated primaries at 0.90
            // to 0.95 value, which made hundreds of crowd capsules the brightest, busiest
            // thing on screen - brighter than the actual cast - so the eye went to the
            // scenery and the players read as specks. They are a backdrop: same hues,
            // pulled toward the lavender of the stands and dropped in value so the
            // characters win every contrast fight.
            Color[] palette =
            {
                new Color(0.62f,0.48f,0.66f), new Color(0.46f,0.52f,0.76f),
                new Color(0.68f,0.62f,0.58f), new Color(0.48f,0.62f,0.66f),
                new Color(0.58f,0.50f,0.74f), new Color(0.70f,0.56f,0.58f),
            };
            Material[] mats = new Material[palette.Length];
            for (int i = 0; i < palette.Length; i++) mats[i] = Lit($"Crowd{i}", palette[i], 0.2f);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject bank = new GameObject($"Crowd {(side < 0 ? "L" : "R")}");
                for (int row = 0; row < 3; row++)
                    for (float z = fromZ; z < toZ; z += 1.9f)
                    {
                        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        Object.DestroyImmediate(c.GetComponent<Collider>());
                        c.transform.SetParent(bank.transform, false);
                        c.transform.localPosition = new Vector3(
                            side * (laneHalfWidth + 2.2f + row * 1.7f),
                            0.9f + row * 0.75f,
                            z + Random.Range(-0.4f, 0.4f));
                        c.transform.localScale = Vector3.one * Random.Range(0.5f, 0.68f);
                        c.GetComponent<Renderer>().sharedMaterial = mats[Random.Range(0, mats.Length)];
                    }
                bank.AddComponent<CrowdBob>();
            }
        }

        /// <summary>Posts and a beam over the finish - it gives the lane a destination.</summary>
        public static void FinishGantry(float z, float halfWidth)
        {
            Material gold = Lit("Gold", new Color(0.98f, 0.80f, 0.25f), 0.7f, 0.6f,
                                new Color(0.55f, 0.42f, 0.05f));
            GameObject g = new GameObject("FinishGantry");
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(post.GetComponent<Collider>());
                post.transform.SetParent(g.transform, false);
                post.transform.localPosition = new Vector3(s * halfWidth, 3f, z);
                post.transform.localScale = new Vector3(0.5f, 6f, 0.5f);
                post.GetComponent<Renderer>().sharedMaterial = gold;
            }
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.transform.SetParent(g.transform, false);
            beam.transform.localPosition = new Vector3(0f, 6f, z);
            beam.transform.localScale = new Vector3(halfWidth * 2f + 0.5f, 0.6f, 0.5f);
            beam.GetComponent<Renderer>().sharedMaterial = gold;
        }

        /// <summary>
        /// Places a CC0 kit model. DECORATION ONLY - colliders are stripped, because
        /// gameplay collision belongs to the primitives that the hazard scripts drive.
        /// Dressing must never change what the physics does.
        /// </summary>
        public static GameObject Kit(Transform parent, string modelName, Vector3 pos,
                                     Quaternion rot, float scale)
        {
            string path = $"Assets/_Party/Art/Kenney/Kit/{modelName}.fbx";
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) { Debug.LogWarning($"[Party] kit model missing: {modelName}"); return null; }
            // Object.Instantiate, NOT PrefabUtility.InstantiatePrefab. The latter creates a
            // linked prefab INSTANCE carrying modification data, and building the scene
            // with those produced a player that died at startup with "level0 is corrupted"
            // - same signature as the volume-profile bug: a MonoBehaviour deserialising
            // past the end of the data. A plain copy has no linkage to serialise.
            GameObject g = Object.Instantiate(src);
            if (parent != null) g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.rotation = rot;
            g.transform.localScale = Vector3.one * scale;
            foreach (var c in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            return g;
        }

        /// <summary>Side walls so the lane feels like a course rather than an edge of nothing.</summary>
        public static void Barriers(float halfWidth, float fromZ, float toZ)
        {
            Material m = Lit("Barrier", new Color(0.92f, 0.94f, 0.97f), 0.25f);
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = $"Barrier {(s < 0 ? "L" : "R")}";
                b.transform.position = new Vector3(s * halfWidth, 0.6f, (fromZ + toZ) * 0.5f);
                b.transform.localScale = new Vector3(0.4f, 1.2f, toZ - fromZ);
                b.GetComponent<Renderer>().sharedMaterial = m;
            }
        }
    }
}
