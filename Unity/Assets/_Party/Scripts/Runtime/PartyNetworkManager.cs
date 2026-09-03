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

        [Tooltip("Spawn ACTIVE RAGDOLLS rather than the single-capsule look. Ten synced " +
                 "transforms per player instead of one - set per scene while the cost is " +
                 "being measured.")]
        public bool spawnRagdolls;

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

        [Header("Server-spawned singletons")]
        [Tooltip("Prefabs spawned once when the server starts - round directors and the like. " +
                 "They must be PREFABS: Mirror's scene post-processor disables any scene object " +
                 "carrying a NetworkIdentity, which silently kills it in a build.")]
        public GameObject[] serverSpawnOnStart;

        [Header("Transport selection")]
        [Tooltip("Direct-IP transport. Used for local and LAN testing.")]
        public Transport localTransport;
        [Tooltip("Steam P2P transport. Used when Steam is available and not overridden.")]
        public Transport steamTransport;

        readonly List<PartyPlayer> _bots = new List<PartyPlayer>();

        // STABLE IDENTITY. Slots are claimed and released, never just incremented.
        // With a running counter the bots were renamed and recoloured every time one was
        // destroyed and refilled, so "Bevel" in round 1 was a different capsule in round
        // 2 - which destroys the one thing the host is for. Barnaby's running gags depend
        // on a name meaning the same participant all night.
        readonly HashSet<int> _usedSlots = new HashSet<int>();
        int _nextSlot;

        int ClaimSlot()
        {
            for (int i = 0; i < 8; i++)
                if (_usedSlots.Add(i)) return i;
            return _nextSlot++ % 8;   // should never happen: 8 participants maximum
        }

        void ReleaseSlot(int slot) => _usedSlots.Remove(slot);

        /// <summary>
        /// Choose the transport before Mirror binds it in base.Awake().
        ///
        /// Steam when it is genuinely up, direct-IP otherwise. This keeps every local and
        /// LAN test working on a machine with no Steam at all - which matters, because
        /// Steam P2P needs two Steam-capable machines and cannot be exercised solo.
        /// Force either with -transport steam | -transport local.
        /// </summary>
        public override void Awake()
        {
            string forced = null;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-transport") forced = args[i + 1].ToLowerInvariant();

            bool useSteam = forced == "steam" || (forced == null && SteamBoot.Ready);

            if (useSteam && steamTransport != null)
            {
                transport = steamTransport;
                if (!SteamBoot.Ready)
                    Debug.LogError("[Party] -transport steam requested but Steam is not ready: "
                                   + SteamBoot.FailureReason);
            }
            else
            {
                transport = localTransport != null ? localTransport : transport;
            }

            if (steamTransport != null) steamTransport.enabled = transport == steamTransport;
            if (localTransport != null) localTransport.enabled = transport == localTransport;

            Debug.Log($"[Party] transport = {(transport != null ? transport.GetType().Name : "NONE")}"
                      + $" (steamReady={SteamBoot.Ready})");

            base.Awake();
        }

        /// <summary>
        /// Acts on whatever the menu chose.
        ///
        /// This lived in a separate MatchBootstrap component until adding that component
        /// to the scene produced a player that died at startup with "level0 is corrupted"
        /// - reproducible: with the component the build was corrupt, without it the scene
        /// ran. The script itself was clean (valid meta, one class, no duplicate types),
        /// so the fault was in Unity's scene serialisation rather than anything readable
        /// in the code. Folding the logic into a component the scene already has avoids
        /// the whole problem.
        /// </summary>
        public override void Start()
        {
            base.Start();

            targetParticipants = Mathf.Clamp(Character.PendingSetup.participants, 2, 8);
            SteamLobby lobby = GetComponent<SteamLobby>();

            switch (Character.PendingSetup.Consume())
            {
                case Character.PendingSetup.Mode.HostLocal:
                    StartHost();
                    RedLight.RedLightDirector.Instance?.BeginRound();
                    break;
                case Character.PendingSetup.Mode.HostSteam:
                    if (lobby != null) lobby.HostLobby(targetParticipants);
                    break;
                case Character.PendingSetup.Mode.JoinCode:
                    if (lobby != null) lobby.JoinByCode(Character.PendingSetup.code);
                    break;
                case Character.PendingSetup.Mode.JoinAddress:
                    networkAddress = Character.PendingSetup.address;
                    StartClient();
                    break;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _bots.Clear();
            _usedSlots.Clear();
            _nextSlot = 0;

            if (serverSpawnOnStart != null)
                foreach (GameObject prefab in serverSpawnOnStart)
                {
                    if (prefab == null) continue;
                    NetworkServer.Spawn(Instantiate(prefab));
                    Debug.Log($"[Party] server-spawned {prefab.name}");
                }

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

            int slot = ClaimSlot();
            PartyPlayer p = go.GetComponent<PartyPlayer>();
            p.isBot       = false;
            p.useRagdoll  = spawnRagdolls;
            p.slot        = slot;
            p.displayName = "Player " + (conn.connectionId);
            p.colour      = Palette[slot % Palette.Length];
            // The host wears what it chose in the menu. Remote clients send their own
            // look after connecting; until then they get a slot-derived default.
            p.lookPacked  = conn.connectionId == 0
                ? PartyPlayer.Pack(Character.PlayerProfile.Look)
                : PartyPlayer.Pack(DefaultLook(slot));

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

                int slot = ClaimSlot();
                PartyPlayer p = go.GetComponent<PartyPlayer>();
                p.isBot       = true;
                p.useRagdoll  = spawnRagdolls;
                p.slot        = slot;
                p.displayName = BotNames[slot % BotNames.Length];
                p.colour      = Palette[slot % Palette.Length];
                p.lookPacked  = PartyPlayer.Pack(DefaultLook(slot));

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
                ReleaseSlot(b.slot);
                NetworkServer.Destroy(b.gameObject);
                return;
            }
        }

        /// <summary>A distinct but deterministic look per slot, so bots are telling apart.</summary>
        static Character.LookConfig DefaultLook(int slot) => new Character.LookConfig
        {
            chassis   = slot % 8,
            livery    = slot % 5,
            filament  = slot % 6,
            shape     = slot % 5,
            dome      = slot % 4,
            mask      = (slot * 3) % 5,
            accessory = (slot * 2) % 5,
        };

        int CountParticipants()
        {
            _bots.RemoveAll(b => b == null);
            return _bots.Count + NetworkServer.connections.Count;
        }
    }
}
