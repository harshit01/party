// One MonoBehaviour per file, named after the class.
//
// PosterMotion.cs held three of them. Unity resolves serialised script references BY FILE
// NAME, so a component in a file named after something else cannot be read back out of a
// scene - the cause of the corrupt builds this project fought from August to September.
// These live in the Menu scene, which had the same exposure.
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
}
