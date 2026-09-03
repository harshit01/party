// One MonoBehaviour per file, named after the class - see SweeperBar.cs.
using UnityEngine;

namespace Party.Character
{
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
