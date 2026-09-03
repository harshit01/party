using UnityEngine;

namespace Party.Ragdoll
{
    /// <summary>
    /// Puts the Filament's face back on the ragdoll.
    ///
    /// The ragdoll rebuild traded the character away for the physics: what stands and falls
    /// over is anatomically right but is nine bare capsules, and the Filament - wide dome,
    /// white eyes, cone nose, and a glowing wire inside the dome whose brightness encodes
    /// standing with Barnaby - was left behind in CharacterLook.
    ///
    /// EVERYTHING HERE IS COSMETIC: no colliders, no rigidbodies, parented to the physical
    /// bones so it inherits their motion for free. Adding mass or collision to a face would
    /// change how the body falls, and the falling is the part that took four attempts to get
    /// right.
    ///
    /// The glowing wire is the one piece that is not decoration. HANDOFF §3 says the Filament
    /// exists so Barnaby's favouritism is visible in the room rather than buried in a server
    /// log - a framed player has to be able to tell cheating from their own mistake.
    /// </summary>
    public static class RagdollDress
    {
        public static void Apply(RagdollBuilder.Rig rig, Color livery, float scale = 1f)
        {
            Transform head = rig.Get(Bone.Head)?.transform;
            Transform chest = rig.Get(Bone.Chest)?.transform;
            if (head == null || chest == null) return;

            Material white = Mat(new Color(0.97f, 0.98f, 1f), 0.2f);
            Material dark  = Mat(new Color(0.09f, 0.12f, 0.22f), 0.1f);
            Material nose  = Mat(new Color(1f, 0.70f, 0.32f), 0.3f);
            Material trim  = Mat(livery * 0.75f, 0.35f);

            // THE HEAD *IS* THE DOME.
            //
            // The first attempt kept the opaque head sphere and added a second translucent
            // shell over it, which just reads as one big white ball - the glowing wire, the
            // whole reason the Filament exists, was sealed inside something you cannot see
            // through. So the physics head takes the glass material directly and the wire
            // lives inside it.
            Renderer headR = head.GetComponent<Renderer>();
            if (headR != null) headR.sharedMaterial = DomeMaterial();

            // Eyes sit on the FRONT of the head, and the head is what the camera reads first -
            // a body with a face reads as a character even when it is face down on the floor.
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject eye = Deco(PrimitiveType.Sphere, head, s < 0 ? "EyeL" : "EyeR",
                    new Vector3(s * 0.21f, 0.02f, 0.34f), Vector3.one * 0.34f, white);
                Deco(PrimitiveType.Sphere, eye.transform, "Pupil",
                    new Vector3(0f, 0f, 0.44f), Vector3.one * 0.55f, dark);
            }

            Deco(PrimitiveType.Cylinder, head, "Nose",
                new Vector3(0f, -0.14f, 0.42f), new Vector3(0.11f, 0.13f, 0.11f), nose)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // THE FILAMENT, inside the dome and now actually visible through it. Bloom is
            // already in the post-processing profile, so an emissive value above 1 blooms.
            GameObject wire = Deco(PrimitiveType.Capsule, head, "Filament",
                new Vector3(0f, 0.10f, -0.02f), new Vector3(0.13f, 0.30f, 0.13f),
                WireMaterial());
            wire.transform.localRotation = Quaternion.Euler(18f, 0f, 12f);

            var glow = wire.AddComponent<RagdollFilament>();
            glow.wire = wire.GetComponent<Renderer>();

            // Collar, so the head does not look like a balloon on a stick.
            Deco(PrimitiveType.Cylinder, chest, "Collar",
                new Vector3(0f, 0.52f, 0f), new Vector3(0.86f, 0.10f, 0.86f), trim);
        }

        static GameObject Deco(PrimitiveType t, Transform parent, string name,
                               Vector3 pos, Vector3 scale, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(t);
            go.name = name;
            // NO COLLIDER. A face that collides changes how the body falls.
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>
        /// Load the persisted dome material, falling back to a runtime one.
        ///
        /// The fallback exists so the editor and the lab still show SOMETHING if the asset
        /// is missing, but it is not equivalent: a runtime-built transparent material comes
        /// out opaque in a player, because Unity strips the shader variants nothing in the
        /// build references. That is exactly what made the dome a solid white ball with the
        /// glowing wire sealed inside it.
        /// </summary>
        static Material DomeMaterial() =>
            Resources.Load<Material>("DomeGlass") ?? Glass(new Color(0.80f, 0.90f, 1f));

        static Material WireMaterial() =>
            Resources.Load<Material>("Filament") ?? Emissive(new Color(1f, 0.84f, 0.38f));

        static Material Mat(Color c, float smooth)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            m.SetFloat("_Smoothness", smooth);
            return m;
        }

        /// <summary>
        /// URP transparency needs ALL of this, not just an alpha below 1.
        ///
        /// Setting the colour's alpha and the surface float was not enough - the dome
        /// rendered fully opaque, because URP picks its blend path from shader KEYWORDS and
        /// the render queue, not from the alpha value. Missing any one of them leaves an
        /// opaque object with a misleading inspector.
        /// </summary>
        static Material Glass(Color c)
        {
            Material m = Mat(new Color(c.r, c.g, c.b, 0.26f), 0.9f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_Surface", 1f);   // 0 opaque, 1 transparent
            m.SetFloat("_Blend", 0f);     // alpha blend
            m.SetFloat("_ZWrite", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        static Material Emissive(Color c)
        {
            Material m = Mat(c, 0.5f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 3f);
            return m;
        }
    }

}
