using UnityEngine;

namespace Party.Character
{
    /// <summary>
    /// The glowing wire inside the dome. This is the character's entire face.
    ///
    /// Built from small emissive segments rather than a LineRenderer so it lights the
    /// dome from inside and reads in 3D from any angle.
    ///
    /// STANDING WITH THE HOST IS VISIBLE HERE. Barnaby's affinity drives brightness and
    /// steadiness: a favourite burns warm and even, a grudge dims and flickers. His bias
    /// otherwise lives only in a server log, which means a framed player cannot tell
    /// whether they were cheated or simply moved. Putting it on the face makes the
    /// unfairness legible - which is the point of the mechanic, not a decoration.
    /// </summary>
    public class FilamentRig : MonoBehaviour
    {
        [Header("Live state")]
        [Range(-1f, 1f)] public float standing;      // -1 grudge .. +1 favourite
        public FilamentMood mood = FilamentMood.Idle;

        Material _mat;
        Color    _base;
        Transform[] _segments;
        float _flickerSeed;

        public void Build(int shape, Color glow)
        {
            foreach (Transform c in transform) DestroyImmediate(c.gameObject);

            _base = glow;
            _mat  = CharacterLook.Emissive(glow, 2.2f);
            _flickerSeed = Random.Range(0f, 100f);

            Vector3[] pts = Glyph(shape);
            _segments = new Transform[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "Seg" + i;
                DestroyImmediate(s.GetComponent<Collider>());
                s.transform.SetParent(transform, false);
                s.transform.localPosition = pts[i];
                s.transform.localScale = Vector3.one * 0.062f;
                s.GetComponent<Renderer>().sharedMaterial = _mat;
                _segments[i] = s.transform;
            }
        }

        /// <summary>
        /// The glyph is the personality. Each is a single continuous wire, never two eyes -
        /// that is the whole reason this is not another two-dot blob.
        /// </summary>
        static Vector3[] Glyph(int shape)
        {
            var list = new System.Collections.Generic.List<Vector3>();
            switch (shape)
            {
                case 1: // Zigzag
                    for (int i = 0; i < 11; i++)
                        list.Add(new Vector3(-0.22f + i * 0.044f, (i % 2 == 0 ? 0.06f : -0.06f), 0f));
                    break;
                case 2: // Loop
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i / 14f * Mathf.PI * 2f;
                        list.Add(new Vector3(Mathf.Cos(a) * 0.16f, Mathf.Sin(a) * 0.11f, 0f));
                    }
                    break;
                case 3: // Wave
                    for (int i = 0; i < 13; i++)
                    {
                        float t = i / 12f;
                        list.Add(new Vector3(-0.24f + t * 0.48f, Mathf.Sin(t * Mathf.PI * 2f) * 0.08f, 0f));
                    }
                    break;
                case 4: // Split - two arcs that still form one line
                    for (int i = 0; i < 6; i++)
                        list.Add(new Vector3(-0.24f + i * 0.05f, Mathf.Sin(i / 5f * Mathf.PI) * 0.09f, 0f));
                    for (int i = 0; i < 6; i++)
                        list.Add(new Vector3(0.06f + i * 0.05f, -Mathf.Sin(i / 5f * Mathf.PI) * 0.09f, 0f));
                    break;
                default: // Coil
                    for (int i = 0; i < 16; i++)
                    {
                        float t = i / 15f;
                        list.Add(new Vector3(-0.22f + t * 0.44f,
                                             Mathf.Sin(t * Mathf.PI * 4f) * 0.075f,
                                             Mathf.Cos(t * Mathf.PI * 4f) * 0.03f));
                    }
                    break;
            }
            return list.ToArray();
        }

        void Update()
        {
            if (_mat == null || _segments == null) return;

            // Favour burns steady and bright; a grudge gutters.
            float warmth = Mathf.InverseLerp(-1f, 1f, standing);          // 0..1
            float steady = Mathf.Lerp(0.35f, 1f, warmth);
            float flicker = 1f;
            if (standing < -0.1f)
            {
                float n = Mathf.PerlinNoise(_flickerSeed, Time.time * 9f);
                flicker = Mathf.Lerp(1f, Mathf.Lerp(0.25f, 1f, n), -standing);
            }

            float moodMul = mood switch
            {
                FilamentMood.Frozen  => 0.55f,   // holding perfectly still
                FilamentMood.Running => 1.25f,
                FilamentMood.Smug    => 1.6f,
                FilamentMood.Broken  => 0.15f,
                _ => 1f,
            };

            Color c = Color.Lerp(_base * 0.7f, _base, warmth);
            _mat.SetColor("_EmissionColor", c * (2.2f * steady * flicker * moodMul));
            _mat.SetColor("_BaseColor", c);

            // The wire itself moves: agitated when running, dead flat when frozen.
            float agitation = mood switch
            {
                FilamentMood.Running => 0.020f,
                FilamentMood.Smug    => 0.012f,
                FilamentMood.Broken  => 0.030f,
                FilamentMood.Frozen  => 0f,
                _ => 0.006f,
            };
            if (agitation <= 0f) return;
            for (int i = 0; i < _segments.Length; i++)
            {
                Vector3 p = _segments[i].localPosition;
                p.y += Mathf.Sin(Time.time * 11f + i * 0.9f + _flickerSeed) * agitation * Time.deltaTime * 60f * 0.016f;
                _segments[i].localPosition = p;
            }
        }
    }
}
