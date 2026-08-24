using Mirror;
using Party.RedLight;
using UnityEngine;

namespace Party.Character
{
    /// <summary>
    /// Acts on whatever the menu chose. Without this the front end sets its intent and
    /// the game scene ignores it, so PLAY silently does nothing.
    /// </summary>
    public class MatchBootstrap : MonoBehaviour
    {
        PartyNetworkManager _nm;
        SteamLobby _lobby;

        void Start()
        {
            _nm = GetComponent<PartyNetworkManager>();
            _lobby = GetComponent<SteamLobby>();
            if (_nm == null) return;

            _nm.targetParticipants = Mathf.Clamp(PendingSetup.participants, 2, 8);

            switch (PendingSetup.Consume())
            {
                case PendingSetup.Mode.HostLocal:
                    _nm.StartHost();
                    RedLightDirector.Instance?.BeginRound();
                    break;
                case PendingSetup.Mode.HostSteam:
                    if (_lobby != null) _lobby.HostLobby(_nm.targetParticipants);
                    break;
                case PendingSetup.Mode.JoinCode:
                    if (_lobby != null) _lobby.JoinByCode(PendingSetup.code);
                    break;
                case PendingSetup.Mode.JoinAddress:
                    _nm.networkAddress = PendingSetup.address;
                    _nm.StartClient();
                    break;
            }
        }
    }
}
