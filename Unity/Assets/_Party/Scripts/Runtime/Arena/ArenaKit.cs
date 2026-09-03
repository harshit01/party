using Mirror;
using UnityEngine;

namespace Party.Arena
{
    /// <summary>
    /// The shared clock every piece of arena motion runs on.
    ///
    /// NETWORK TIME, NOT Time.time. Two machines must draw a swinging hammer in the same
    /// place without syncing a transform for it - a hazard that is somewhere else on your
    /// screen than on mine is worse than a static one. This is the rule the Red Light
    /// sweeper already followed, generalised so every animated prop obeys it.
    ///
    /// NOTE: this file holds no MonoBehaviours on purpose. Unity resolves serialised script
    /// references by file name, so a MonoBehaviour living in a file named after something
    /// else cannot be deserialised out of a scene - which is the "level0 is corrupted"
    /// failure this codebase has now hit three times. One component, one file, same name.
    /// </summary>
    public static class ArenaTime
    {
        public static double Now =>
            NetworkClient.active || NetworkServer.active ? NetworkTime.time : Time.timeAsDouble;
    }
}
