using UnityEngine;

namespace Party.Character
{
    /// <summary>
    /// The front-end set: a rotating sunburst, drifting confetti and pulsing spotlights.
    ///
    /// A menu backed by an empty skybox reads as a tech demo. This is a TELEVISED GAME
    /// SHOW, so the front end should look like a studio with the lights on - and all of
    /// it is primitives and unlit colour, so it still costs no art.
    /// </summary>
    public class MenuStage : MonoBehaviour
    {
        [Header("Backdrop")]
        public Transform sunburst;          // optional, unused by the soft stage
        public float sunburstSpeed = 3.5f;

        [Header("Bokeh")]
        [Tooltip("Soft out-of-focus dots. Assigned by MenuSetup.")]
        public Material[] bokehMaterials;
        public int confettiCount = 34;
        public Vector3 area = new Vector3(16f, 10f, 6f);
        public float fallSpeed = 0.55f;

        [Header("Lights")]
        public Light[] pulseLights;
        public float pulseSpeed = 1.4f;
        public float pulseAmount = 0.35f;

        Transform[] _confetti;
        Vector3[]   _spin;
        float[]     _baseIntensity;

        void Start()
        {
            SpawnConfetti();
            if (pulseLights != null)
            {
                _baseIntensity = new float[pulseLights.Length];
                for (int i = 0; i < pulseLights.Length; i++)
                    if (pulseLights[i] != null) _baseIntensity[i] = pulseLights[i].intensity;
            }
        }

        void SpawnConfetti()
        {
            // Few, large, soft and slow. Ninety hard chips read as debris; a few dozen
            // out-of-focus lights read as atmosphere.
            Material[] mats = (bokehMaterials != null && bokehMaterials.Length > 0)
                ? bokehMaterials
                : new[] { Unlit(new Color(1f, 0.8f, 0.6f, 0.25f)) };

            GameObject root = new GameObject("Bokeh");
            root.transform.SetParent(transform, false);
            _confetti = new Transform[confettiCount];
            _spin = new Vector3[confettiCount];

            for (int i = 0; i < confettiCount; i++)
            {
                GameObject c = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(c.GetComponent<Collider>());
                c.transform.SetParent(root.transform, false);
                c.transform.localPosition = RandomPos(true);
                float sz = Random.Range(0.35f, 1.5f);
                c.transform.localScale = new Vector3(sz, sz, 1f);
                c.transform.localRotation = Quaternion.identity;   // billboards face camera
                c.GetComponent<Renderer>().sharedMaterial = mats[Random.Range(0, mats.Length)];
                _confetti[i] = c.transform;
                _spin[i] = new Vector3(Random.Range(-90f, 90f), Random.Range(-90f, 90f), Random.Range(-90f, 90f));
            }
        }

        Vector3 RandomPos(bool anywhere) => new Vector3(
            Random.Range(-area.x * 0.5f, area.x * 0.5f),
            anywhere ? Random.Range(-area.y * 0.5f, area.y * 0.5f) : area.y * 0.5f,
            Random.Range(-area.z * 0.5f, area.z * 0.5f));

        void Update()
        {
            if (sunburst != null)
                sunburst.Rotate(Vector3.forward, sunburstSpeed * Time.deltaTime, Space.Self);

            if (_confetti != null)
                for (int i = 0; i < _confetti.Length; i++)
                {
                    Transform t = _confetti[i];
                    if (t == null) continue;
                    Vector3 p = t.localPosition;
                    p.y -= fallSpeed * Time.deltaTime * (0.18f + (i % 5) * 0.06f);   // drift, not fall
                    p.x += Mathf.Sin(Time.time * 0.35f + i) * 0.18f * Time.deltaTime;
                    if (p.y < -area.y * 0.5f) p = RandomPos(false);
                    t.localPosition = p;
                    if (Camera.main != null) t.forward = Camera.main.transform.forward;
                }

            if (pulseLights != null && _baseIntensity != null)
                for (int i = 0; i < pulseLights.Length; i++)
                {
                    if (pulseLights[i] == null) continue;
                    float w = Mathf.Sin(Time.time * pulseSpeed + i * 1.7f) * pulseAmount;
                    pulseLights[i].intensity = _baseIntensity[i] * (1f + w);
                }
        }

        public static Material Unlit(Color c)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            Material m = new Material(sh);
            m.SetColor("_BaseColor", c);
            return m;
        }
    }
}
