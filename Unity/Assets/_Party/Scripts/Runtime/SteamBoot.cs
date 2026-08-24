#if !DISABLESTEAMWORKS
using Steamworks;
#endif
using UnityEngine;

namespace Party
{
    /// <summary>
    /// Brings the Steam API up and keeps its callbacks pumping.
    ///
    /// The UPM build of Steamworks.NET ships the API only - no SteamManager - so this
    /// is that job, done deliberately.
    ///
    /// FAILURES ARE LOUD (HANDOFF.md section 6.2). A silent Steam init failure would
    /// present later as "lobbies mysteriously do not work", which is exactly the kind of
    /// confident, meaningless result that cost this project three test runs before.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SteamBoot : MonoBehaviour
    {
        public static bool Ready { get; private set; }
        public static string FailureReason { get; private set; } = "not attempted";

        // A plain static guard, NOT a GameObject singleton. SteamBoot shares a GameObject
        // with NetworkManager, so destroying a "duplicate" would take the whole
        // networking object with it.
        static bool _initialised;

#if !DISABLESTEAMWORKS
        void Awake()
        {
            if (_initialised) return;   // never touch this GameObject's lifetime
            _initialised = true;

            if (!Packsize.Test())
                Debug.LogError("[Steam] Packsize.Test failed - wrong Steamworks.NET binary for this platform.");
            if (!DllCheck.Test())
                Debug.LogError("[Steam] DllCheck.Test failed - Steam DLLs are the wrong version.");

            try
            {
                Ready = SteamAPI.Init();
            }
            catch (System.DllNotFoundException e)
            {
                Ready = false;
                FailureReason = "steam_api native library not found: " + e.Message;
                Debug.LogError("[Steam] " + FailureReason);
                return;
            }

            if (!Ready)
            {
                FailureReason =
                    "SteamAPI.Init() returned false. Usual causes: the Steam client is not " +
                    "running, you are not signed in, or steam_appid.txt is missing next to " +
                    "the executable.";
                Debug.LogError("[Steam] " + FailureReason);
                return;
            }

            FailureReason = "";
            Debug.Log($"[Steam] ready. user={SteamFriends.GetPersonaName()} id={SteamUser.GetSteamID()}");
        }

        void Update()
        {
            if (Ready) SteamAPI.RunCallbacks();
        }

        void OnDestroy()
        {
            if (!Ready) return;
            SteamAPI.Shutdown();
            Ready = false;
            _initialised = false;
        }
#else
        void Awake()
        {
            FailureReason = "built with DISABLESTEAMWORKS";
            Debug.LogError("[Steam] " + FailureReason);
        }
#endif
    }
}
