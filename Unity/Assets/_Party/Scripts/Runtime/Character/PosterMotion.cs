using UnityEngine;

namespace Party.Character
{
    /// <summary>
    /// Makes the menu read as a MOVING POSTER: composed like key art, but nothing is
    /// ever completely still.
    ///
    /// Three things do the work, and none of them add objects:
    ///   CAMERA DRIFT   - a slow figure-of-eight sway, so the frame breathes
    ///   PARALLAX       - layers shift at different rates against that sway, which is
    ///                    what creates the sense of depth in a poster
    ///   CONSTANT LIFE  - bunting sways, rays sweep, the hero bobs
    ///
    /// A still composition with one spinning object reads as a turntable. Everything
    /// moving slightly, at different speeds, reads as a living frame.
    /// </summary>
    public class PosterMotion : MonoBehaviour
    {
        [Header("Camera drift")]
        public Transform cam;
        public float swayX = 0.42f, swayY = 0.16f;
        public float swaySpeed = 0.13f;
        public float pushDepth = 0.35f;

        [Header("Parallax")]
        [Tooltip("Furthest first. Each layer moves less than the one in front of it.")]
        public Transform[] layers;
        public float parallaxStrength = 0.55f;

        Vector3 _camHome;
        Vector3[] _layerHome;

        void Start()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam != null) _camHome = cam.localPosition;
            if (layers != null)
            {
                _layerHome = new Vector3[layers.Length];
                for (int i = 0; i < layers.Length; i++)
                    if (layers[i] != null) _layerHome[i] = layers[i].localPosition;
            }
        }

        void LateUpdate()
        {
            float t = Time.time * swaySpeed;
            // Figure of eight: x and y on different harmonics never repeat obviously.
            float ox = Mathf.Sin(t * Mathf.PI * 2f) * swayX;
            float oy = Mathf.Sin(t * Mathf.PI * 4f) * swayY;
            float oz = Mathf.Sin(t * Mathf.PI * 1.3f) * pushDepth;

            if (cam != null)
            {
                cam.localPosition = _camHome + new Vector3(ox, oy, oz);
                cam.localRotation = Quaternion.Euler(
                    4.5f - oy * 1.2f, 11f + ox * 1.4f, ox * 0.5f);
            }

            if (layers == null || _layerHome == null) return;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                // Nearer layers (higher index) counter-move more.
                float depth = (i + 1) / (float)layers.Length;
                layers[i].localPosition = _layerHome[i] +
                    new Vector3(-ox * parallaxStrength * depth,
                                -oy * parallaxStrength * depth * 0.6f, 0f);
            }
        }
    }

    /// <summary>Bunting that sways like a string in a breeze, each flag lagging the last.</summary>
    public class BuntingSway : MonoBehaviour
    {
        public float amplitude = 5.5f;
        public float speed = 1.1f;

        Transform[] _flags;
        float[] _phase;

        void Start()
        {
            int n = transform.childCount;
            _flags = new Transform[n];
            _phase = new float[n];
            for (int i = 0; i < n; i++)
            {
                _flags[i] = transform.GetChild(i);
                _phase[i] = i * 0.42f;          // travelling wave along the string
            }
        }

        void Update()
        {
            if (_flags == null) return;
            for (int i = 0; i < _flags.Length; i++)
            {
                if (_flags[i] == null) continue;
                float a = Mathf.Sin(Time.time * speed + _phase[i]) * amplitude;
                _flags[i].localRotation = Quaternion.Euler(0f, 0f, 180f + a);
            }
        }
    }

    /// <summary>
    /// Soft light rays sweeping across the frame. Additive, unlit, and slow - the thing
    /// that most reliably turns a flat backdrop into something that feels lit.
    /// </summary>
    public class LightRays : MonoBehaviour
    {
        public float speed = 2.4f;
        public float breathe = 0.18f;

        Transform[] _rays;
        float[] _phase;
        Vector3[] _baseScale;

        void Start()
        {
            int n = transform.childCount;
            _rays = new Transform[n];
            _phase = new float[n];
            _baseScale = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                _rays[i] = transform.GetChild(i);
                _phase[i] = Random.Range(0f, 10f);
                _baseScale[i] = _rays[i].localScale;
            }
        }

        void Update()
        {
            if (_rays == null) return;
            transform.Rotate(Vector3.forward, speed * Time.deltaTime, Space.Self);
            for (int i = 0; i < _rays.Length; i++)
            {
                if (_rays[i] == null) continue;
                float w = 1f + Mathf.Sin(Time.time * 0.6f + _phase[i]) * breathe;
                Vector3 s = _baseScale[i];
                _rays[i].localScale = new Vector3(s.x * w, s.y, s.z);
            }
        }
    }
}
