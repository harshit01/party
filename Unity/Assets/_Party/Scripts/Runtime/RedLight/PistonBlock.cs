// One MonoBehaviour per file, named after the class - see SweeperBar.cs.
using Mirror;
using UnityEngine;

namespace Party.RedLight
{
    /// <summary>
    /// A pusher that only moves while Barnaby has called GO.
    ///
    /// This is the point of a hazard in THIS game rather than a generic obstacle course:
    /// during STOP the lane goes still, so the danger and the freeze are the same beat.
    /// A hazard that kept shoving during STOP would make you move and get you eliminated
    /// for something you did not do - unfair in the way the network must never be, as
    /// opposed to unfair in the way Barnaby is supposed to be.
    /// </summary>
    public class PistonBlock : MonoBehaviour
    {
        public float travel = 3.5f;
        public float speed  = 1.4f;
        public float phaseOffset;

        Vector3 _home;

        void Start() => _home = transform.localPosition;

        void Update()
        {
            RedLightDirector d = RedLightDirector.Instance;
            bool moving = d == null || d.phase == RoundPhase.Go;
            if (!moving) return;

            double t = NetworkClient.active || NetworkServer.active ? NetworkTime.time : Time.timeAsDouble;
            float x = Mathf.Sin((float)t * speed + phaseOffset) * travel;

            transform.localPosition = _home + new Vector3(x, 0f, 0f);
        }
    }
}
