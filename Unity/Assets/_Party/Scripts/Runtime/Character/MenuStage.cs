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
        [Header("Sunburst")]
        public Transform sunburst;
        public float sunburstSpeed = 3.5f;

        [Header("Confetti")]
        public int confettiCount = 90;
        public Vector3 area = new Vector3(16f, 10f, 6f);
        public float fallSpeed = 0.9f;

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
            Color[] cols =
            {
                new Color(0.98f,0.30f,0.45f), new Color(0.35f,0.70f,1f), new Color(0.35f,0.90f,0.55f),
                new Color(1f,0.82f,0.25f), new Color(0.72f,0.45f,1f), new Color(1f,0.60f,0.25f),
            };
            Material[] mats = new Material[cols.Length];
            for (int i = 0; i < cols.Length; i++) mats[i] = Unlit(cols[i]);

            GameObject root = new GameObject("Confetti");
            root.transform.SetParent(transform, false);
            _confetti = new Transform[confettiCount];
            _spin = new Vector3[confettiCount];

            for (int i = 0; i < confettiCount; i++)
            {
                GameObject c = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(c.GetComponent<Collider>());
                c.transform.SetParent(root.transform, false);
                c.transform.localPosition = RandomPos(true);
                c.transform.localScale = new Vector3(Random.Range(0.09f, 0.20f), Random.Range(0.12f, 0.26f), 1f);
                c.transform.localRotation = Random.rotation;
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
                    p.y -= fallSpeed * Time.deltaTime * (0.6f + (i % 5) * 0.16f);
                    p.x += Mathf.Sin(Time.time * 0.7f + i) * 0.10f * Time.deltaTime;
                    if (p.y < -area.y * 0.5f) p = RandomPos(false);
                    t.localPosition = p;
                    t.Rotate(_spin[i] * Time.deltaTime, Space.Self);
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
