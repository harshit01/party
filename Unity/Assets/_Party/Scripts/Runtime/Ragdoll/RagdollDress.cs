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

            // Dome: a translucent shell over the head sphere, slightly proud of it.
            GameObject dome = Deco(PrimitiveType.Sphere, head, "Dome",
                new Vector3(0f, 0.06f, 0f), Vector3.one * 1.22f, Glass(new Color(0.85f, 0.93f, 1f)));

            // Eyes sit on the FRONT of the head, and the head is what the camera reads first -
            // a body with a face reads as a character even when it is face down on the floor.
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject eye = Deco(PrimitiveType.Sphere, head, s < 0 ? "EyeL" : "EyeR",
                    new Vector3(s * 0.23f, 0.06f, 0.40f), Vector3.one * 0.40f, white);
                Deco(PrimitiveType.Sphere, eye.transform, "Pupil",
                    new Vector3(0f, 0f, 0.42f), Vector3.one * 0.52f, dark);
            }

            Deco(PrimitiveType.Cylinder, head, "Nose",
                new Vector3(0f, -0.08f, 0.46f), new Vector3(0.13f, 0.13f, 0.13f), nose)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // THE FILAMENT. Emissive, and its brightness is standing with Barnaby.
            GameObject wire = Deco(PrimitiveType.Capsule, head, "Filament",
                new Vector3(0f, 0.16f, 0f), new Vector3(0.10f, 0.26f, 0.10f),
                Emissive(new Color(1f, 0.84f, 0.38f)));

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

        static Material Mat(Color c, float smooth)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            m.SetFloat("_Smoothness", smooth);
            return m;
        }

        static Material Glass(Color c)
        {
            Material m = Mat(new Color(c.r, c.g, c.b, 0.30f), 0.85f);
            m.SetFloat("_Surface", 1f);            // transparent
            m.SetFloat("_Blend", 0f);
            m.renderQueue = 3000;
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
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

    /// <summary>Brightness = standing with Barnaby, pulsing when he is fond of you.</summary>
    public class RagdollFilament : MonoBehaviour
    {
        public Renderer wire;
        [Range(-1f, 1f)] public float standing;

        MaterialPropertyBlock _mpb;

        void LateUpdate()
        {
            if (wire == null) return;
            _mpb ??= new MaterialPropertyBlock();

            // A pet burns steady and bright; a grudge gutters. Same signal the HUD meter
            // shows, but readable from across a room without reading anything.
            float t = Mathf.InverseLerp(-1f, 1f, standing);
            float flicker = standing < -0.35f
                ? 0.55f + 0.45f * Mathf.PerlinNoise(Time.time * 9f, 0f)
                : 1f;
            Color c = Color.Lerp(new Color(1f, 0.35f, 0.30f), new Color(1f, 0.88f, 0.45f), t);

            wire.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", c * (0.6f + 3.2f * t) * flicker);
            _mpb.SetColor("_BaseColor", c);
            wire.SetPropertyBlock(_mpb);
        }
    }
}
