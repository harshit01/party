using UnityEngine;

namespace Party.Juice
{
    /// <summary>
    /// Bobs a bank of crowd primitives out of phase with each other.
    ///
    /// A stadium of perfectly still blocks looks broken; the same blocks bobbing look
    /// like an audience. Local-only, no netcode, no art.
    /// </summary>
    public class CrowdBob : MonoBehaviour
    {
        public float amplitude = 0.18f;
        public float speed = 2.2f;

        Transform[] _members;
        float[] _phase;
        float[] _baseY;

        void Start()
        {
            int n = transform.childCount;
            _members = new Transform[n];
            _phase = new float[n];
            _baseY = new float[n];
            for (int i = 0; i < n; i++)
            {
                _members[i] = transform.GetChild(i);
                _phase[i] = Random.Range(0f, Mathf.PI * 2f);
                _baseY[i] = _members[i].localPosition.y;
            }
        }

        void Update()
        {
            if (_members == null) return;
            for (int i = 0; i < _members.Length; i++)
            {
                Vector3 p = _members[i].localPosition;
                p.y = _baseY[i] + Mathf.Sin(Time.time * speed + _phase[i]) * amplitude;
                _members[i].localPosition = p;
            }
        }
    }
}
