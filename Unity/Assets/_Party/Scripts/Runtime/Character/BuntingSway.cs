// One MonoBehaviour per file, named after the class - see SweeperBar.cs.
using UnityEngine;

namespace Party.Character
{
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
}
