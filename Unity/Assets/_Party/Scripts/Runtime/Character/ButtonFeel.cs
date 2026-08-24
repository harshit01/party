using UnityEngine;
using UnityEngine.EventSystems;

namespace Party.Character
{
    /// <summary>
    /// Hover lift and click punch. Static buttons feel like a form; a button that
    /// answers you feels like a game, and it costs nothing.
    /// </summary>
    public class ButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        public float hoverScale = 1.045f;
        public float speed = 12f;

        Vector3 _base = Vector3.one;
        float _target = 1f;
        float _punch;

        void Awake() => _base = transform.localScale;

        public void OnPointerEnter(PointerEventData e) { _target = hoverScale; MenuAudio.Instance?.Hover(); }
        public void OnPointerExit(PointerEventData e)  => _target = 1f;
        public void OnPointerDown(PointerEventData e)  { _punch = 1f; MenuAudio.Instance?.Click(); }

        void Update()
        {
            _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 6f);
            float s = Mathf.Lerp(transform.localScale.x / Mathf.Max(_base.x, 0.0001f), _target,
                                 Time.unscaledDeltaTime * speed) - _punch * 0.06f;
            transform.localScale = _base * s;
        }
    }
}
