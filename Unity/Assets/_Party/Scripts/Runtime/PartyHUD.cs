using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Party
{
    /// <summary>
    /// Throwaway OnGUI panel for the milestone: host, join by code, join by address.
    /// Replaced by real UI once the round loop exists - it is here so the netcode can be
    /// driven by hand without building menus first.
    ///
    /// Shows Steam's actual state rather than hiding it. A lobby that silently does
    /// nothing because Steam never initialised is the exact failure this project has
    /// been bitten by before.
    /// </summary>
    public class PartyHUD : MonoBehaviour
    {
        PartyNetworkManager _nm;
        SteamLobby _lobby;
        string _code = "";
        string _address = "localhost";

        /// <summary>
        /// Edited locally and pushed to the manager only when you actually start a
        /// session. It is NOT bound straight to targetParticipants: OnGUI runs a Layout
        /// pass in which controls have no width, and a slider bound directly returned its
        /// minimum every frame and silently clamped the participant count to 2.
        /// </summary>
        int _target = 4;

        void Awake()
        {
            _nm = GetComponent<PartyNetworkManager>();
            _lobby = GetComponent<SteamLobby>();
            _target = Mathf.Clamp(_nm.targetParticipants, 2, 8);
        }

        /// <summary>
        /// Hidden once a session is actually running, toggled with F3.
        ///
        /// This panel is a debugging instrument, not part of the game, and it was sitting
        /// permanently in the top-left corner of the playable screen showing transport
        /// names and Steam failure text. It is still needed - you cannot host or join
        /// without it until real menus exist - so it shows while you are unconnected and
        /// gets out of the way the moment a round starts.
        /// </summary>
        bool _show = true;

        bool _showForced;

        void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null || !k.f3Key.wasPressedThisFrame) return;

            if (NetworkClient.active || NetworkServer.active) _showForced = !_showForced;
            else _show = !_show;
        }

        void OnGUI()
        {
            bool inSession = NetworkClient.active || NetworkServer.active;
            if (inSession && !_showForced) return;
            if (!_show) return;

            GUILayout.BeginArea(new Rect(10, 10, 330, 320), GUI.skin.box);

            GUILayout.Label($"<b>Party — netcode milestone</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"transport: {(Transport.active != null ? Transport.active.GetType().Name : "none")}");
            GUILayout.Label($"steam: {(SteamBoot.Ready ? "ready" : "NOT READY - " + SteamBoot.FailureReason)}");

            if (!NetworkClient.active && !NetworkServer.active)
            {
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"participants: {_target}");
                if (GUILayout.Button("-", GUILayout.Width(28))) _target = Mathf.Max(2, _target - 1);
                if (GUILayout.Button("+", GUILayout.Width(28))) _target = Mathf.Min(8, _target + 1);
                GUILayout.EndHorizontal();
                GUILayout.Label($"you + {_target - 1} bots if nobody joins");

                if (GUILayout.Button("Host (local)"))
                {
                    _nm.targetParticipants = _target;
                    _nm.StartHost();
                }

                GUILayout.Space(4);
                GUILayout.Label("Join by address:");
                GUILayout.BeginHorizontal();
                _address = GUILayout.TextField(_address);
                if (GUILayout.Button("Join", GUILayout.Width(60)))
                {
                    _nm.networkAddress = _address;
                    _nm.StartClient();
                }
                GUILayout.EndHorizontal();

                if (_lobby != null)
                {
                    GUILayout.Space(8);
                    GUI.enabled = SteamBoot.Ready;
                    if (GUILayout.Button("Host on Steam (get join code)"))
                    {
                        _nm.targetParticipants = _target;
                        _lobby.HostLobby(_target);
                    }

                    GUILayout.Label("Join by code:");
                    GUILayout.BeginHorizontal();
                    _code = GUILayout.TextField(_code.ToUpperInvariant(), 5);
                    if (GUILayout.Button("Go", GUILayout.Width(60))) _lobby.JoinByCode(_code);
                    GUILayout.EndHorizontal();
                    GUI.enabled = true;

                    if (!SteamBoot.Ready)
                        GUILayout.Label("<i>Steam buttons need the Steam client running.</i>",
                                        new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(SteamLobby.CurrentCode))
                    GUILayout.Label($"<size=20><b>JOIN CODE: {SteamLobby.CurrentCode}</b></size>",
                                    new GUIStyle(GUI.skin.label) { richText = true });
                GUILayout.Label($"lobby: {SteamLobby.Status}");
                GUILayout.Label(NetworkServer.active
                    ? $"HOST — {NetworkServer.connections.Count} connection(s)"
                    : $"CLIENT — connected: {NetworkClient.isConnected}");

                if (GUILayout.Button("Stop"))
                {
                    if (_lobby != null) _lobby.Leave();
                    if (NetworkServer.active) _nm.StopHost(); else _nm.StopClient();
                }
            }

            GUILayout.EndArea();
        }
    }
}
