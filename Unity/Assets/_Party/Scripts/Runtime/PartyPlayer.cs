using Mirror;
using UnityEngine;

namespace Party
{
    /// <summary>
    /// One participant's capsule. Human or bot - the rest of the game cannot tell.
    ///
    /// HOST-AUTHORITATIVE (HANDOFF.md section 1). Clients send input, never position:
    /// the host is the only thing that moves a Rigidbody, and NetworkTransform pushes
    /// the result out. That costs a round trip of input latency and buys a host that
    /// cannot be lied to - which matters for a game about shoving people off things.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PartyPlayer : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] float moveForce = 45f;
        [SerializeField] float maxSpeed  = 7f;

        [SyncVar(hook = nameof(OnNameChanged))]   public string displayName = "?";
        [SyncVar(hook = nameof(OnColourChanged))] public Color  colour      = Color.white;

        /// <summary>True for a slot filled by a bot. Synced so clients can label it.</summary>
        [SyncVar(hook = nameof(OnNameChanged))]   public bool   isBot;

        /// <summary>Called out by Barnaby - fairly or otherwise. Cannot move.</summary>
        [SyncVar(hook = nameof(OnStateChanged))]  public bool   eliminated;
        /// <summary>Crossed the line.</summary>
        [SyncVar(hook = nameof(OnStateChanged))]  public bool   finished;

        [Header("Scene refs")]
        [SerializeField] Renderer  bodyRenderer;
        [SerializeField] TextMesh  nameTag;

        Rigidbody   _rb;
        IMoveInput  _input;      // host-side only
        Vector2     _pendingMove; // last input received for this player, host-side

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        public override void OnStartServer()
        {
            // A bot drives itself on the host. A human's input arrives by command.
            // Bot policy is per-minigame. Red Light needs freeze-on-stop behaviour;
            // without a director (the bare netcode scene) they just wander.
            if (isBot)
                _input = RedLight.RedLightDirector.Instance != null
                    ? new RedLight.RedLightBotInput(transform)
                    : (IMoveInput)new BotMoveInput(transform);
        }

        public override void OnStartClient()
        {
            ApplyName();
            ApplyColour();
        }

        /// <summary>
        /// TEST ONLY. With -partyautopilot, a local human's capsule is driven by the bot
        /// policy instead of the keyboard, so a headless build can exercise the real
        /// input -> CmdMove -> host -> NetworkTransform path. Without it, a headless
        /// client reads no devices and its capsule sits still, which would let a broken
        /// input path pass a sync test unnoticed.
        /// </summary>
        static bool? _autopilot;
        static bool Autopilot
        {
            get
            {
                _autopilot ??= System.Array.IndexOf(
                    System.Environment.GetCommandLineArgs(), "-partyautopilot") >= 0;
                return _autopilot.Value;
            }
        }

        void Update()
        {
            // Only the human who owns this capsule samples devices, and only to send
            // intent. isLocalPlayer is false on every bot, so bots never read a keyboard.
            if (!isLocalPlayer) return;

            _input ??= Autopilot ? new BotMoveInput(transform) : (IMoveInput)new LocalMoveInput();
            CmdMove(_input.Move);
        }

        [Command(channel = Channels.Unreliable)]
        void CmdMove(Vector2 move)
        {
            // Never trust a client's magnitude.
            _pendingMove = Vector2.ClampMagnitude(move, 1f);
        }

        [Server]
        public void ServerTeleport(Vector3 position)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.position = position;
            // Tell NetworkTransform this is a jump, not motion to interpolate through.
            GetComponent<NetworkTransformBase>()?.Reset();
        }

        void FixedUpdate()
        {
            if (!isServer) return;   // host-authoritative: only the host integrates physics

            // Bots produce their input here; humans' arrived via CmdMove.
            if (isBot && _input != null) _pendingMove = _input.Move;

            // A round may forbid movement entirely (eliminated, finished, countdown).
            // Note this does NOT stop a player pressing keys during STOP - moving when
            // told to freeze is exactly the mistake the game is about.
            RedLight.RedLightDirector director = RedLight.RedLightDirector.Instance;
            if (director != null && !director.MovementAllowed(this))
            {
                _pendingMove = Vector2.zero;
                Vector3 v = _rb.linearVelocity;
                _rb.linearVelocity = new Vector3(v.x * 0.6f, v.y, v.z * 0.6f);
                return;
            }

            Vector3 dir = new Vector3(_pendingMove.x, 0f, _pendingMove.y);
            if (dir.sqrMagnitude > 0.0001f) _rb.AddForce(dir * moveForce, ForceMode.Acceleration);

            Vector3 flat = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            if (flat.magnitude > maxSpeed)
            {
                flat = flat.normalized * maxSpeed;
                _rb.linearVelocity = new Vector3(flat.x, _rb.linearVelocity.y, flat.z);
            }
        }

        void LateUpdate()
        {
            // Billboard the label at whatever camera is rendering.
            if (nameTag == null) return;
            Camera c = Camera.main;
            if (c != null) nameTag.transform.rotation = c.transform.rotation;
        }

        void OnStateChanged(bool _, bool __) { ApplyName(); ApplyColour(); }
        void OnNameChanged(string _, string __) => ApplyName();
        void OnNameChanged(bool _, bool __)     => ApplyName();
        void OnColourChanged(Color _, Color __) => ApplyColour();

        void ApplyName()
        {
            if (nameTag == null) return;
            string suffix = eliminated ? "  ✖" : finished ? "  ★" : isBot ? " (bot)" : "";
            nameTag.text = displayName + suffix;
        }

        void ApplyColour()
        {
            // Eliminated players go grey so being out is readable at a glance across a
            // room, which is the only way anyone reads a party game screen.
            Color c = eliminated ? new Color(0.35f, 0.35f, 0.38f) : colour;
            if (bodyRenderer != null) bodyRenderer.material.color = c;
            if (nameTag != null)      nameTag.color = c;
        }
    }
}
