using Mirror;
using UnityEngine;

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

        void Awake()
        {
            _nm = GetComponent<PartyNetworkManager>();
            _lobby = GetComponent<SteamLobby>();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 330, 320), GUI.skin.box);

            GUILayout.Label($"<b>Party — netcode milestone</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"transport: {(Transport.active != null ? Transport.active.GetType().Name : "none")}");
            GUILayout.Label($"steam: {(SteamBoot.Ready ? "ready" : "NOT READY - " + SteamBoot.FailureReason)}");

            if (!NetworkClient.active && !NetworkServer.active)
            {
                GUILayout.Space(6);
                _nm.targetParticipants = Mathf.RoundToInt(
                    GUILayout.HorizontalSlider(_nm.targetParticipants, 2, 8));
                GUILayout.Label($"participants: {_nm.targetParticipants} " +
                                $"(you + {_nm.targetParticipants - 1} bots if nobody joins)");

                if (GUILayout.Button("Host (local)")) _nm.StartHost();

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
                        _lobby.HostLobby(_nm.targetParticipants);

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
