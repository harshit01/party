#if !DISABLESTEAMWORKS
using Steamworks;
#endif
using Mirror;
using UnityEngine;

namespace Party
{
    /// <summary>
    /// Steam lobbies with a short JOIN CODE.
    ///
    /// Join codes rather than friend invites, deliberately. HANDOFF.md section 1 asks for
    /// both, and codes are the half that works today: a brand-new Steam account is
    /// "limited" until money has been spent on it, and limited accounts CANNOT ADD
    /// FRIENDS. An invite-only flow would be untestable on a fresh second account.
    ///
    /// How the code works: the lobby is public and carries the code in its metadata. A
    /// joiner filters the lobby list on that exact key. No server, no directory, nothing
    /// to host - which keeps the "no hosting bill" property intact.
    /// </summary>
    public class SteamLobby : MonoBehaviour
    {
        public const string KeyCode = "party_code";
        public const string KeyHost = "party_host";

        [Tooltip("Characters used in join codes. No 0/O/1/I - they get misread aloud.")]
        const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        const int CodeLength = 5;

        public static string CurrentCode { get; private set; } = "";
        public static string Status { get; private set; } = "idle";

        NetworkManager _nm;

#if !DISABLESTEAMWORKS
        CSteamID _lobby;
        Callback<LobbyCreated_t>       _onCreated;
        Callback<LobbyEnter_t>         _onEntered;
        Callback<GameLobbyJoinRequested_t> _onJoinRequested;
        CallResult<LobbyMatchList_t>   _onList;
        string _pendingCode = "";

        void Awake()
        {
            _nm = GetComponent<NetworkManager>();
            _onCreated       = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _onEntered       = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            _onJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            _onList          = CallResult<LobbyMatchList_t>.Create(OnLobbyList);
        }

        static string NewCode()
        {
            var sb = new System.Text.StringBuilder(CodeLength);
            for (int i = 0; i < CodeLength; i++)
                sb.Append(CodeAlphabet[Random.Range(0, CodeAlphabet.Length)]);
            return sb.ToString();
        }

        // ---------------- hosting ----------------

        public void HostLobby(int maxPlayers = 8)
        {
            if (!Require()) return;
            Status = "creating lobby";
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, Mathf.Clamp(maxPlayers, 2, 8));
        }

        void OnLobbyCreated(LobbyCreated_t e)
        {
            if (e.m_eResult != EResult.k_EResultOK)
            {
                Status = "lobby creation FAILED: " + e.m_eResult;
                Debug.LogError("[Lobby] " + Status);
                return;
            }

            _lobby = new CSteamID(e.m_ulSteamIDLobby);
            CurrentCode = NewCode();

            SteamMatchmaking.SetLobbyData(_lobby, KeyCode, CurrentCode);
            SteamMatchmaking.SetLobbyData(_lobby, KeyHost, SteamUser.GetSteamID().ToString());

            _nm.StartHost();
            Status = "hosting";
            Debug.Log($"[Lobby] hosting. JOIN CODE = {CurrentCode}  (lobby {_lobby})");
        }

        // ---------------- joining ----------------

        public void JoinByCode(string code)
        {
            if (!Require()) return;
            _pendingCode = (code ?? "").Trim().ToUpperInvariant();
            if (_pendingCode.Length == 0) { Debug.LogError("[Lobby] empty join code"); return; }

            Status = "searching for " + _pendingCode;
            Debug.Log("[Lobby] " + Status);
            SteamMatchmaking.AddRequestLobbyListStringFilter(
                KeyCode, _pendingCode, ELobbyComparison.k_ELobbyComparisonEqual);
            _onList.Set(SteamMatchmaking.RequestLobbyList());
        }

        void OnLobbyList(LobbyMatchList_t e, bool failed)
        {
            if (failed || e.m_nLobbiesMatching == 0)
            {
                Status = $"no lobby found for code {_pendingCode}";
                Debug.LogError("[Lobby] " + Status);
                return;
            }
            SteamMatchmaking.JoinLobby(SteamMatchmaking.GetLobbyByIndex(0));
        }

        // Accepting an invite from the Steam overlay, for when accounts can be friends.
        void OnJoinRequested(GameLobbyJoinRequested_t e) => SteamMatchmaking.JoinLobby(e.m_steamIDLobby);

        void OnLobbyEntered(LobbyEnter_t e)
        {
            _lobby = new CSteamID(e.m_ulSteamIDLobby);
            if (NetworkServer.active) return;   // the host enters its own lobby too

            string host = SteamMatchmaking.GetLobbyData(_lobby, KeyHost);
            if (string.IsNullOrEmpty(host))
            {
                Status = "lobby has no host id - cannot connect";
                Debug.LogError("[Lobby] " + Status);
                return;
            }

            CurrentCode = SteamMatchmaking.GetLobbyData(_lobby, KeyCode);
            _nm.networkAddress = host;          // FizzySteamworks dials a SteamID
            _nm.StartClient();
            Status = "joining " + host;
            Debug.Log($"[Lobby] entered lobby {_lobby}, connecting to host {host}");
        }

        public void Leave()
        {
            if (_lobby.IsValid()) SteamMatchmaking.LeaveLobby(_lobby);
            _lobby = CSteamID.Nil;
            CurrentCode = "";
            Status = "idle";
        }

        bool Require()
        {
            if (SteamBoot.Ready) return true;
            Status = "Steam not ready: " + SteamBoot.FailureReason;
            Debug.LogError("[Lobby] " + Status);
            return false;
        }
#else
        void Awake() => Debug.LogError("[Lobby] built with DISABLESTEAMWORKS - no lobbies.");
        public void HostLobby(int maxPlayers = 8) { }
        public void JoinByCode(string code) { }
        public void Leave() { }
#endif
    }
}
