using UnityEngine;

namespace Party.Character
{
    /// <summary>How the filament is drawn - this is the character's entire face.</summary>
    public enum FilamentMood { Idle, Running, Frozen, Smug, Broken }

    /// <summary>One contestant's appearance. Everything is an index so it syncs in a few bytes.</summary>
    [System.Serializable]
    public struct LookConfig
    {
        public int chassis;    // body enamel
        public int livery;     // "clothes" - panel pattern
        public int filament;   // glow colour
        public int shape;      // filament glyph
        public int dome;       // glass tint
        public int mask;       // over the dome
        public int accessory;  // aerial, pack, collar

        public static LookConfig Default => new LookConfig { chassis = 0, livery = 0, filament = 0, shape = 0, dome = 0, mask = 0, accessory = 0 };
    }

    /// <summary>
    /// THE FILAMENT - this game's contestant, built entirely from primitives.
    ///
    /// A wide bell-jar dome on a brass collar over a rounded enamel chassis. Inside the
    /// dome is ONE GLOWING WIRE, and that wire is the whole face: no eyes, no mouth. It
    /// changes shape with state - a nervous squiggle while running, a dead flat line
    /// during STOP, a smug spiral on a win, a jagged break when eliminated.
    ///
    /// WHY THIS SHAPE AND NOT A BEAN: uniqueness is a hard requirement in HANDOFF.md, and
    /// a rounded two-eyed blob is the most copied silhouette in the genre.
    ///
    /// WHY IT EARNS ITS KEEP: the filament's brightness and colour encode the player's
    /// standing with Barnaby. His favourites burn warm and steady; someone he has taken
    /// against flickers and dims BEFORE he calls them out. His bias currently exists only
    /// in a server log, so a framed player cannot tell cheating from their own mistake.
    /// On the character, the whole room can see he has favourites.
    /// </summary>
    public static class CharacterLook
    {
        public static readonly Color[] Chassis =
        {
            new Color(0.93f,0.27f,0.42f), new Color(0.20f,0.55f,0.88f), new Color(0.22f,0.76f,0.48f),
            new Color(0.97f,0.74f,0.18f), new Color(0.62f,0.36f,0.86f), new Color(0.97f,0.50f,0.16f),
            new Color(0.16f,0.78f,0.74f), new Color(0.88f,0.88f,0.92f),
        };
        public static readonly string[] ChassisNames =
        { "Crimson", "Cobalt", "Fern", "Amber", "Violet", "Ember", "Lagoon", "Bone" };

        public static readonly Color[] Filament =
        {
            new Color(1.00f,0.82f,0.42f), new Color(0.55f,0.95f,1.00f), new Color(1.00f,0.45f,0.55f),
            new Color(0.60f,1.00f,0.60f), new Color(0.85f,0.60f,1.00f), new Color(1.00f,1.00f,1.00f),
        };
        public static readonly string[] FilamentNames =
        { "Tungsten", "Arc", "Neon", "Phosphor", "Aether", "Cold" };

        public static readonly string[] ShapeNames  = { "Coil", "Zigzag", "Loop", "Wave", "Split" };
        public static readonly string[] LiveryNames = { "Plain", "Racer", "Panel", "Halved", "Speckle" };
        public static readonly string[] DomeNames   = { "Clear", "Smoke", "Rose", "Sea" };
        public static readonly string[] MaskNames   = { "None", "Visor", "Cage", "Blinkers", "Snorkel" };
        public static readonly string[] AccessoryNames = { "None", "Aerial", "Pack", "Collar", "Fin" };

        const string LookName = "Look";

        public static Transform Build(Transform parent, LookConfig cfg, out Renderer chassisRenderer,
                                      out FilamentRig rig)
        {
            Transform old = parent.Find(LookName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            GameObject root = new GameObject(LookName);
            root.transform.SetParent(parent, false);

            Color body  = Chassis[Wrap(cfg.chassis, Chassis.Length)];
            Color glow  = Filament[Wrap(cfg.filament, Filament.Length)];
            Material enamel = Mat(body, 0.42f);
            Material dark   = Mat(body * 0.45f, 0.35f);
            Material brass  = Mat(new Color(0.78f, 0.62f, 0.32f), 0.8f, 0.7f);

            // ---- chassis ----
            GameObject chassis = Prim(PrimitiveType.Capsule, root.transform, "Chassis");
            chassis.transform.localScale = new Vector3(0.70f, 0.62f, 0.70f);   // slimmer
            chassis.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            chassisRenderer = chassis.GetComponent<Renderer>();
            chassisRenderer.sharedMaterial = enamel;

            BuildLivery(root.transform, cfg.livery, body);

            // ---- limbs ----
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject arm = Prim(PrimitiveType.Capsule, root.transform, s < 0 ? "ArmL" : "ArmR");
                arm.transform.localPosition = new Vector3(s * 0.36f, -0.16f, 0f);
                arm.transform.localScale = new Vector3(0.15f, 0.24f, 0.15f);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, s * 16f);
                arm.GetComponent<Renderer>().sharedMaterial = dark;

                GameObject leg = Prim(PrimitiveType.Capsule, root.transform, s < 0 ? "LegL" : "LegR");
                leg.transform.localPosition = new Vector3(s * 0.17f, -0.76f, 0f);
                leg.transform.localScale = new Vector3(0.18f, 0.20f, 0.18f);
                leg.GetComponent<Renderer>().sharedMaterial = dark;
            }

            // ---- collar + dome ----
            GameObject collar = Prim(PrimitiveType.Cylinder, root.transform, "Collar");
            collar.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            collar.transform.localScale = new Vector3(0.44f, 0.07f, 0.44f);
            collar.GetComponent<Renderer>().sharedMaterial = brass;

            GameObject dome = Prim(PrimitiveType.Sphere, root.transform, "Dome");
            dome.transform.localPosition = new Vector3(0f, 0.44f, 0f);
            dome.transform.localScale = new Vector3(0.66f, 0.58f, 0.66f);
            dome.GetComponent<Renderer>().sharedMaterial = Glass(DomeTint(cfg.dome));

            // ---- the filament: the whole face ----
            GameObject fil = new GameObject("Filament");
            fil.transform.SetParent(root.transform, false);
            fil.transform.localPosition = new Vector3(0f, 0.44f, 0.06f);
            rig = fil.AddComponent<FilamentRig>();
            rig.Build(Wrap(cfg.shape, ShapeNames.Length), glow);

            BuildFace(root.transform);
            BuildMask(root.transform, cfg.mask, body);
            BuildAccessory(root.transform, cfg.accessory, body, brass, glow);
            return root.transform;
        }

        /// <summary>
        /// White eyes, dark pupils and a cone nose on the front of the dome.
        ///
        /// The original design had no eyes at all - the filament was the whole face. The
        /// founder's reference is a clean minimal face, so eyes and a nose go on the
        /// outside and the filament stays inside as the glow that shows standing with the
        /// host. The face makes it read as a character; the glow keeps the mechanic.
        /// </summary>
        static void BuildFace(Transform root)
        {
            Material white = Mat(new Color(0.99f, 0.99f, 1f), 0.55f);
            Material pupil = Mat(new Color(0.06f, 0.06f, 0.09f), 0.6f);
            Material nose  = Mat(new Color(0.99f, 0.72f, 0.42f), 0.35f);

            for (int s = -1; s <= 1; s += 2)
            {
                GameObject eye = Prim(PrimitiveType.Sphere, root, s < 0 ? "EyeL" : "EyeR");
                eye.transform.localPosition = new Vector3(s * 0.15f, 0.50f, 0.27f);
                eye.transform.localScale = Vector3.one * 0.20f;
                eye.GetComponent<Renderer>().sharedMaterial = white;

                GameObject pu = Prim(PrimitiveType.Sphere, eye.transform, "Pupil");
                pu.transform.localPosition = new Vector3(0f, 0f, 0.30f);
                pu.transform.localScale = Vector3.one * 0.55f;
                pu.GetComponent<Renderer>().sharedMaterial = pupil;
            }

            // Cone nose - Unity has no cone primitive, so the mesh is generated.
            GameObject n = new GameObject("Nose");
            n.transform.SetParent(root, false);
            n.transform.localPosition = new Vector3(0f, 0.42f, 0.30f);
            n.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // point forward
            n.transform.localScale = new Vector3(0.14f, 0.20f, 0.14f);
            n.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            n.AddComponent<MeshRenderer>().sharedMaterial = nose;
        }

        static Mesh _cone;
        static Mesh ConeMesh()
        {
            if (_cone != null) return _cone;
            _cone = new Mesh { name = "NoseCone" };
            var verts = new System.Collections.Generic.List<Vector3> { new Vector3(0f, 1f, 0f) };
            const int seg = 18;
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f));
            }
            verts.Add(Vector3.zero);
            int baseC = verts.Count - 1;
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < seg; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % seg;
                tris.Add(0); tris.Add(b); tris.Add(a);
                tris.Add(baseC); tris.Add(a); tris.Add(b);
            }
            _cone.SetVertices(verts); _cone.SetTriangles(tris, 0);
            _cone.RecalculateNormals(); _cone.RecalculateBounds();
            return _cone;
        }

        // ---------- parts ----------

        static void BuildLivery(Transform root, int livery, Color body)
        {
            Material light = Mat(Color.Lerp(body, Color.white, 0.62f), 0.5f);
            Material dark  = Mat(body * 0.4f, 0.4f);
            switch (Wrap(livery, LiveryNames.Length))
            {
                case 1: // Racer - a stripe over the shoulder
                    var st = Prim(PrimitiveType.Cube, root, "Stripe");
                    st.transform.localPosition = new Vector3(0f, -0.16f, 0.36f);
                    st.transform.localScale = new Vector3(0.16f, 0.62f, 0.10f);
                    st.GetComponent<Renderer>().sharedMaterial = light;
                    break;
                case 2: // Panel - a chest plate
                    var pl = Prim(PrimitiveType.Cube, root, "Panel");
                    pl.transform.localPosition = new Vector3(0f, -0.20f, 0.34f);
                    pl.transform.localScale = new Vector3(0.46f, 0.34f, 0.10f);
                    pl.GetComponent<Renderer>().sharedMaterial = dark;
                    break;
                case 3: // Halved
                    var hv = Prim(PrimitiveType.Capsule, root, "Half");
                    hv.transform.localPosition = new Vector3(0.24f, -0.18f, 0f);
                    hv.transform.localScale = new Vector3(0.5f, 0.56f, 0.9f);
                    hv.GetComponent<Renderer>().sharedMaterial = light;
                    break;
                case 4: // Speckle
                    for (int i = 0; i < 6; i++)
                    {
                        var sp = Prim(PrimitiveType.Sphere, root, "Speck" + i);
                        float a = i / 6f * Mathf.PI * 2f;
                        sp.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.34f, -0.16f + Mathf.Sin(a) * 0.2f, Mathf.Sin(a) * 0.3f);
                        sp.transform.localScale = Vector3.one * 0.14f;
                        sp.GetComponent<Renderer>().sharedMaterial = light;
                    }
                    break;
            }
        }

        static void BuildMask(Transform root, int mask, Color body)
        {
            Material metal = Mat(new Color(0.35f, 0.36f, 0.40f), 0.7f, 0.55f);
            switch (Wrap(mask, MaskNames.Length))
            {
                case 1: // Visor band across the dome
                    var v = Prim(PrimitiveType.Cube, root, "Visor");
                    v.transform.localPosition = new Vector3(0f, 0.48f, 0.22f);
                    v.transform.localScale = new Vector3(0.62f, 0.16f, 0.34f);
                    v.GetComponent<Renderer>().sharedMaterial = metal;
                    break;
                case 2: // Cage - bars over the dome
                    for (int i = -1; i <= 1; i++)
                    {
                        var b = Prim(PrimitiveType.Cylinder, root, "Bar" + i);
                        b.transform.localPosition = new Vector3(i * 0.19f, 0.46f, 0.02f);
                        b.transform.localScale = new Vector3(0.045f, 0.34f, 0.045f);
                        b.GetComponent<Renderer>().sharedMaterial = metal;
                    }
                    break;
                case 3: // Blinkers - side plates
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var bl = Prim(PrimitiveType.Cube, root, "Blinker");
                        bl.transform.localPosition = new Vector3(s * 0.33f, 0.46f, 0.06f);
                        bl.transform.localScale = new Vector3(0.10f, 0.34f, 0.34f);
                        bl.GetComponent<Renderer>().sharedMaterial = metal;
                    }
                    break;
                case 4: // Snorkel
                    var tube = Prim(PrimitiveType.Cylinder, root, "Snorkel");
                    tube.transform.localPosition = new Vector3(0.30f, 0.66f, 0f);
                    tube.transform.localScale = new Vector3(0.07f, 0.28f, 0.07f);
                    tube.GetComponent<Renderer>().sharedMaterial = metal;
                    break;
            }
        }

        static void BuildAccessory(Transform root, int acc, Color body, Material brass, Color glow)
        {
            switch (Wrap(acc, AccessoryNames.Length))
            {
                case 1: // Aerial with a glowing tip
                    var stalk = Prim(PrimitiveType.Cylinder, root, "Aerial");
                    stalk.transform.localPosition = new Vector3(0f, 0.86f, 0f);
                    stalk.transform.localScale = new Vector3(0.035f, 0.20f, 0.035f);
                    stalk.GetComponent<Renderer>().sharedMaterial = brass;
                    var tip = Prim(PrimitiveType.Sphere, root, "AerialTip");
                    tip.transform.localPosition = new Vector3(0f, 1.06f, 0f);
                    tip.transform.localScale = Vector3.one * 0.11f;
                    tip.GetComponent<Renderer>().sharedMaterial = Emissive(glow, 1.4f);
                    break;
                case 2: // Backpack
                    var pack = Prim(PrimitiveType.Cube, root, "Pack");
                    pack.transform.localPosition = new Vector3(0f, -0.18f, -0.34f);
                    pack.transform.localScale = new Vector3(0.44f, 0.40f, 0.22f);
                    pack.GetComponent<Renderer>().sharedMaterial = Mat(body * 0.5f, 0.3f);
                    break;
                case 3: // Collar ruff
                    var ruff = Prim(PrimitiveType.Cylinder, root, "Ruff");
                    ruff.transform.localPosition = new Vector3(0f, 0.13f, 0f);
                    ruff.transform.localScale = new Vector3(0.70f, 0.04f, 0.70f);
                    ruff.GetComponent<Renderer>().sharedMaterial = Mat(Color.Lerp(body, Color.white, 0.7f), 0.25f);
                    break;
                case 4: // Dorsal fin
                    var fin = Prim(PrimitiveType.Cube, root, "Fin");
                    fin.transform.localPosition = new Vector3(0f, 0.02f, -0.30f);
                    fin.transform.localScale = new Vector3(0.06f, 0.34f, 0.30f);
                    fin.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
                    fin.GetComponent<Renderer>().sharedMaterial = Mat(body * 0.6f, 0.4f);
                    break;
            }
        }

        static Color DomeTint(int i) => Wrap(i, DomeNames.Length) switch
        {
            1 => new Color(0.45f, 0.45f, 0.50f, 0.30f),
            2 => new Color(0.95f, 0.70f, 0.78f, 0.28f),
            3 => new Color(0.55f, 0.85f, 0.90f, 0.28f),
            _ => new Color(0.92f, 0.95f, 1.00f, 0.20f),
        };

        // ---------- helpers ----------

        public static int Wrap(int v, int n) => ((v % n) + n) % n;

        static GameObject Prim(PrimitiveType t, Transform parent, string name)
        {
            GameObject g = GameObject.CreatePrimitive(t);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            return g;
        }

        public static Material Mat(Color c, float smoothness, float metallic = 0f)
        {
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            return m;
        }

        public static Material Emissive(Color c, float strength)
        {
            Material m = Mat(c, 0.6f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", c * strength);
            return m;
        }

        /// <summary>Transparent URP Lit. Needs the surface keywords, not just an alpha.</summary>
        public static Material Glass(Color tint)
        {
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetFloat("_Surface", 1f);                 // transparent
            m.SetFloat("_Blend", 0f);                   // alpha
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Smoothness", 0.92f);
            m.SetColor("_BaseColor", tint);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }
    }
}
