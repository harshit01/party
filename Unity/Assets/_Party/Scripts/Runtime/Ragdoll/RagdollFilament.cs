// One MonoBehaviour per file, named after the class - see Arena/*.cs for what
// happens when that rule is broken and the component gets serialised into a scene.
using UnityEngine;

namespace Party.Ragdoll
{
    /// <summary>Brightness = standing with Barnaby, pulsing when he is fond of you.</summary>
    public class RagdollFilament : MonoBehaviour
    {
        public Renderer wire;
        [Range(-1f, 1f)] public float standing;

        MaterialPropertyBlock _mpb;

        void LateUpdate()
        {
            if (wire == null) return;
            _mpb ??= new MaterialPropertyBlock();

            // A pet burns steady and bright; a grudge gutters. Same signal the HUD meter
            // shows, but readable from across a room without reading anything.
            float t = Mathf.InverseLerp(-1f, 1f, standing);
            float flicker = standing < -0.35f
                ? 0.55f + 0.45f * Mathf.PerlinNoise(Time.time * 9f, 0f)
                : 1f;
            Color c = Color.Lerp(new Color(1f, 0.35f, 0.30f), new Color(1f, 0.88f, 0.45f), t);

            wire.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", c * (0.6f + 3.2f * t) * flicker);
            _mpb.SetColor("_BaseColor", c);
            wire.SetPropertyBlock(_mpb);
        }
    }
}
