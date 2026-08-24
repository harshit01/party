using UnityEngine;
using UnityEngine.EventSystems;

namespace Party.Character
{
    /// <summary>
    /// Menu sound, generated at runtime - no audio files, same discipline as the art.
    ///
    /// A silent menu is half of why a front end feels unfinished, and procedural blips
    /// are enough to fix that without commissioning a single asset.
    /// </summary>
    public class MenuAudio : MonoBehaviour
    {
        public static MenuAudio Instance { get; private set; }

        AudioSource _src;
        AudioClip _hover, _click;

        void Awake()
        {
            Instance = this;
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _hover = Blip(880f, 0.05f, 0.18f);
            _click = Blip(420f, 0.09f, 0.35f, true);
        }

        /// <summary>A short decaying sine. Square-ish gives it a toy-television edge.</summary>
        static AudioClip Blip(float freq, float seconds, float volume, bool square = false)
        {
            int rate = 44100;
            int n = Mathf.CeilToInt(rate * seconds);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 26f);
                float w = Mathf.Sin(2f * Mathf.PI * freq * t);
                if (square) w = Mathf.Sign(w) * 0.6f + w * 0.4f;
                data[i] = w * env * volume;
            }
            AudioClip c = AudioClip.Create("blip" + freq, n, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }

        public void Hover() { if (_src != null && _hover != null) _src.PlayOneShot(_hover); }
        public void Click() { if (_src != null && _click != null) _src.PlayOneShot(_click); }
    }
}
