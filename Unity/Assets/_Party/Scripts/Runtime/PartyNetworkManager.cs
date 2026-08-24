using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Party
{
    /// <summary>
    /// Fills every unused slot with a bot so a session runs with one real player.
    ///
    /// The rule (HANDOFF.md): 2-8 participants, minimum 2, any slot not taken by a
    /// human is a bot. A human joining takes a bot's place rather than growing the
    /// lobby, so the participant count the host advertises is the count you get.
    /// </summary>
    public class PartyNetworkManager : NetworkManager
    {
        [Header("Party")]
        [Tooltip("Total participants including bots. 2-8.")]
        [Range(2, 8)] public int targetParticipants = 2;

        [Tooltip("Capsule prefab used for bots. Usually the same as playerPrefab.")]
        public GameObject botPrefab;

        static readonly Color[] Palette =
        {
            new Color(0.93f, 0.26f, 0.21f), new Color(0.20f, 0.60f, 0.86f),
            new Color(0.18f, 0.80f, 0.44f), new Color(0.95f, 0.77f, 0.06f),
            new Color(0.61f, 0.35f, 0.71f), new Color(0.90f, 0.49f, 0.13f),
            new Color(0.10f, 0.74f, 0.61f), new Color(0.93f, 0.51f, 0.71f),
        };

        static readonly string[] BotNames =
        {
            "Bevel", "Croucher", "Dimple", "Fenwick",
            "Gusset", "Hobb", "Larkspur", "Mullion",
        };

        readonly List<PartyPlayer> _bots = new List<PartyPlayer>();
        int _nextSlot;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _bots.Clear();
            _nextSlot = 0;
            FillWithBots();
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // A human takes a bot's slot rather than adding to the lobby.
            RemoveOneBot();

            Transform start = GetStartPosition();
            GameObject go = start != null
                ? Instantiate(playerPrefab, start.position, start.rotation)
                : Instantiate(playerPrefab);

            PartyPlayer p = go.GetComponent<PartyPlayer>();
            p.isBot       = false;
            p.displayName = "Player " + (conn.connectionId);
            p.colour      = Palette[_nextSlot++ % Palette.Length];

            NetworkServer.AddPlayerForConnection(conn, go);
            Debug.Log($"[Party] human joined as '{p.displayName}'. participants={CountParticipants()}");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            FillWithBots();   // their slot goes back to a bot
        }

        void FillWithBots()
        {
            GameObject prefab = botPrefab != null ? botPrefab : playerPrefab;
            if (prefab == null)
            {
                Debug.LogError("[Party] no player/bot prefab assigned - cannot fill bots.");
                return;
            }

            int guard = 0;
            while (CountParticipants() < targetParticipants && guard++ < 16)
            {
                Transform start = GetStartPosition();
                GameObject go = start != null
                    ? Instantiate(prefab, start.position, start.rotation)
                    : Instantiate(prefab);

                PartyPlayer p = go.GetComponent<PartyPlayer>();
                p.isBot       = true;
                p.displayName = BotNames[_nextSlot % BotNames.Length];
                p.colour      = Palette[_nextSlot % Palette.Length];
                _nextSlot++;

                NetworkServer.Spawn(go);   // no connection: nobody owns a bot
                _bots.Add(p);
            }
            Debug.Log($"[Party] filled to {CountParticipants()} participants ({_bots.Count} bots).");
        }

        void RemoveOneBot()
        {
            for (int i = _bots.Count - 1; i >= 0; i--)
            {
                if (_bots[i] == null) { _bots.RemoveAt(i); continue; }
                PartyPlayer b = _bots[i];
                _bots.RemoveAt(i);
                NetworkServer.Destroy(b.gameObject);
                return;
            }
        }

        int CountParticipants()
        {
            _bots.RemoveAll(b => b == null);
            return _bots.Count + NetworkServer.connections.Count;
        }
    }
}
