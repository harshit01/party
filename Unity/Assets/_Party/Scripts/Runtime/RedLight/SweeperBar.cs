// One MonoBehaviour per file, named after the class.
//
// SweeperBar and PistonBlock shared Hazards.cs, and Unity resolves serialised script
// references BY FILE NAME - so a component in a file named after something else cannot be
// read back out of a scene. Both of these are baked into the Red Light scene, which is the
// scene that has been intermittently producing a corrupt level0 since August.
using Mirror;
using UnityEngine;

namespace Party.RedLight
{
    /// <summary>
    /// A bar that sweeps across the lane and shoves people.
    ///
    /// HOST-AUTHORITATIVE and DETERMINISTIC. The rotation is driven from NetworkTime, not
    /// local time, so every machine draws the bar in the same place without syncing a
    /// transform every frame. Only the server's physics can actually push a player - the
    /// client copy is visual. Getting this wrong would mean two players seeing the bar in
    /// different places and disagreeing about who got hit.
    /// </summary>
    public class SweeperBar : MonoBehaviour
    {
        public float degreesPerSecond = 55f;
        public float phaseOffset;

        double _heldAt;
        bool   _holding;

        void Update()
        {
            RedLightDirector d = RedLightDirector.Instance;
            double t = NetworkClient.active || NetworkServer.active ? NetworkTime.time : Time.timeAsDouble;

            // FREEZE WITH THE LANE. A bar that kept sweeping during STOP would shove
            // players and get them eliminated for movement they did not cause - unfair
            // in the way the game must never be, as opposed to unfair in the way Barnaby
            // is supposed to be. Pistons already did this; sweepers did not, and every
            // player in the lobby was being wiped out by it.
            bool mustHold = d != null && d.MustFreeze;
            if (mustHold)
            {
                if (!_holding) { _holding = true; _heldAt = t; }
                t = _heldAt;                 // hold the angle where it stopped
            }
            else if (_holding)
            {
                _holding = false;
                _resume += t - _heldAt;      // do not teleport when motion resumes
            }

            transform.localRotation =
                Quaternion.Euler(0f, (float)((t - _resume) * degreesPerSecond) + phaseOffset, 0f);
        }

        double _resume;
    }
}
