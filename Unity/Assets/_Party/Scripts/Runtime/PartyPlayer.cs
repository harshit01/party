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

        /// <summary>Stable participant slot 0-7. Owns this player's name and colour.</summary>
        [SyncVar] public int slot;

        /// <summary>
        /// The Filament's appearance, 7 choices packed 3 bits each into one int.
        ///
        /// Packed rather than seven SyncVars: it is one 21-bit value on the wire instead
        /// of seven separate dirty-bit entries, and appearance changes every round.
        /// </summary>
        [SyncVar(hook = nameof(OnLookChanged))] public int lookPacked;

        /// <summary>
        /// Standing with Barnaby, -1 grudge .. +1 favourite, pushed from BarnabyBias.
        ///
        /// THIS IS WHY THE FILAMENT EXISTS. His bias otherwise lives only in a server log,
        /// so a framed player cannot tell cheating from their own mistake. Wired to the
        /// glow, the whole room sees he has favourites before he ever calls anyone out.
        /// </summary>
        [SyncVar(hook = nameof(OnStandingChanged))] public float standing;

        Character.FilamentRig _filament;

        public static int Pack(Character.LookConfig c) =>
            (c.chassis & 7) | ((c.livery & 7) << 3) | ((c.filament & 7) << 6) |
            ((c.shape & 7) << 9) | ((c.dome & 7) << 12) | ((c.mask & 7) << 15) |
            ((c.accessory & 7) << 18);

        public static Character.LookConfig Unpack(int p) => new Character.LookConfig
        {
            chassis   = p & 7,
            livery    = (p >> 3) & 7,
            filament  = (p >> 6) & 7,
            shape     = (p >> 9) & 7,
            dome      = (p >> 12) & 7,
            mask      = (p >> 15) & 7,
            accessory = (p >> 18) & 7,
        };

        void OnLookChanged(int _, int __) => BuildLook();
        void OnStandingChanged(float _, float now) { if (_filament != null) _filament.standing = now; }

        /// <summary>
        /// Build an ACTIVE RAGDOLL instead of the decorative look, and sync every bone.
        ///
        /// Switched on per-scene rather than replacing the look outright, because the cost
        /// is the open question: one synced transform per player becomes ten, and that has
        /// to be measured before it goes anywhere near the minigames.
        /// </summary>
        /// SyncVar, so it reaches the client in the spawn payload - OnStartClient reads it
        /// to decide what to build, and a plain field would still be false there.
        [SyncVar] public bool useRagdoll;

        Ragdoll.RagdollMuscles _ragdoll;

        void BuildRagdoll()
        {
            Transform host = juiceVisual != null ? juiceVisual : transform;
            var rig = Ragdoll.RagdollBuilder.Build(host, colour);

            _ragdoll = gameObject.GetComponent<Ragdoll.RagdollMuscles>();
            if (_ragdoll == null) _ragdoll = gameObject.AddComponent<Ragdoll.RagdollMuscles>();
            _ragdoll.Bind(rig);

            // ONE sync component, already on the prefab, that writes every bone.
            //
            // The first attempt added a NetworkTransform per bone at runtime. Mirror indexes
            // NetworkBehaviours by component order at spawn, so runtime-added ones break
            // serialisation outright - it threw 128,448 NullReferenceExceptions in thirty
            // seconds. And the bones are built at runtime, so they cannot be on the prefab
            // either. One component writing all ten is both correct and cheaper.
            Ragdoll.RagdollSync sync = GetComponent<Ragdoll.RagdollSync>();
            if (sync != null) sync.Bind(rig);

            // TURN OFF THE ROOT NetworkTransform. With a ragdoll it is syncing a transform
            // nothing renders - the visible body is the ten bones, which RagdollSync already
            // sends - so it is pure duplicated bandwidth on a link that is already carrying
            // 4.7x what the capsule did.
            NetworkTransformBase root = GetComponent<NetworkTransformBase>();
            if (root != null) root.enabled = false;
        }

        void BuildLook()
        {
            Transform host = juiceVisual != null ? juiceVisual : transform;
            Character.CharacterLook.Build(host, Unpack(lookPacked), out Renderer body, out _filament);
            bodyRenderer = body;
            if (_filament != null) _filament.standing = standing;
            ApplyColour();
        }

        /// <summary>
        /// Finishing position once the round is over. 1 = won, higher = knocked out
        /// earlier, 0 = still in.
        ///
        /// MINIGAMES.md is explicit that "a clear winner AND a clear loser" is a design
        /// rule and that THE LOSER MATTERS MORE, because the loser is what the host mocks.
        /// Until now nothing recorded who came last - `eliminated` is a boolean, so by the
        /// end of a round four people were equally "out" and the wooden spoon was
        /// unknowable. #11 "The Prediction" is a bet on exactly that, and the scoring
        /// spine needs it too.
        /// </summary>
        [SyncVar] public int placement;

        /// <summary>
        /// Points across the session. #11 "The Prediction" awards them, and the round loop
        /// will carry them between minigames - a collection of games is not a session
        /// until something persists across them.
        /// </summary>
        [SyncVar] public int score;

        /// <summary>Called out by Barnaby - fairly or otherwise. Cannot move.</summary>
        [SyncVar(hook = nameof(OnStateChanged))]  public bool   eliminated;
        /// <summary>Crossed the line.</summary>
        [SyncVar(hook = nameof(OnStateChanged))]  public bool   finished;

        [Header("Name tag")]
    [Tooltip("Fully opaque closer than this.")]
    [SerializeField] float nameFadeStart = 9f;
    [Tooltip("Hidden beyond this. Keeps the screen clear of distant clutter.")]
    [SerializeField] float nameFadeEnd = 22f;
    /// <summary>
    /// World size per metre of camera distance, so a tag holds a CONSTANT SCREEN SIZE.
    ///
    /// The tag is a world-space TextMesh at characterSize 0.16, which was picked for a
    /// camera 23 metres away. The follow camera sits at 7, so the same text drew about
    /// 3.3x bigger and a cluster of bots near the lens turned into a wall of overlapping
    /// letters across the frame - visibly worse than the wide shot it replaced. Scaling
    /// with distance decouples the tag from wherever the camera happens to be.
    /// </summary>
    [SerializeField] float nameSizePerMetre = 0.026f;

    [Header("Scene refs")]
        [Tooltip("Visual-only child the Filament is built under. Never the networked root.")]
        [SerializeField] Transform juiceVisual;
        [SerializeField] Renderer  bodyRenderer;
        [SerializeField] TextMesh  nameTag;

        Rigidbody   _rb;
        IMoveInput  _input;      // host-side only
        Vector2     _pendingMove; // last input received for this player, host-side

        /// <summary>
        /// Discrete actions for "Say What He Says" (#10). Separate from IMoveInput because
        /// that one answers "which way are you leaning" every frame, and this one answers
        /// "did you just press something" - a held key must produce exactly one step.
        /// </summary>
        SayWhat.IActionInput _actions;

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
            {
                _input = RedLight.RedLightDirector.Instance != null
                    ? new RedLight.RedLightBotInput(transform)
                    : (IMoveInput)new BotMoveInput(transform);

                // Bot policy is per-minigame. #10 needs recall, not steering.
                if (SayWhat.SayWhatDirector.Instance != null)
                    _actions = new SayWhat.SayWhatBotInput();
            }
        }

        public override void OnStartClient()
        {
            if (useRagdoll) { BuildRagdoll(); ApplyName(); return; }
            BuildLook();
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
        /// <summary>
        /// TEST ONLY. Exposed so the Prediction wrapper can make autopilot humans bet
        /// like bots - otherwise the human slot never places a bet and a headless test
        /// exercises the betting path for nobody.
        /// </summary>
        public static bool AutopilotEnabled => Autopilot;

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

            // "Say What He Says": send discrete actions as they are pressed. Autopilot
            // drives them with the bot policy so a headless build exercises the real
            // input -> CmdPerform -> server path, exactly as -partyautopilot does for
            // movement; without it a human slot never performs and a broken input path
            // would pass a test unnoticed.
            if (SayWhat.SayWhatDirector.Instance == null) return;
            _actions ??= Autopilot
                ? new SayWhat.SayWhatBotInput()
                : (SayWhat.IActionInput)new SayWhat.LocalActionInput();

            SayWhat.PartyAction a = _actions.Poll();
            if (a != SayWhat.PartyAction.None) CmdPerform((byte)a);
        }

        [Command(channel = Channels.Unreliable)]
        void CmdMove(Vector2 move)
        {
            // Never trust a client's magnitude.
            _pendingMove = Vector2.ClampMagnitude(move, 1f);
        }

        /// <summary>Place a secret bet on who comes last (#11). Reliable: it is a decision.</summary>
        [Command(channel = Channels.Reliable)]
        public void CmdBet(uint targetNetId)
        {
            Prediction.PredictionDirector d = Prediction.PredictionDirector.Instance;
            if (d == null) return;
            foreach (PartyPlayer p in Prediction.PredictionDirector.Players())
                if (p.netId == targetNetId) { d.PlaceBet(this, p); return; }
        }

        [Command(channel = Channels.Reliable)]
        void CmdPerform(byte action)
        {
            // RELIABLE, unlike CmdMove. A dropped movement packet is a missed frame; a
            // dropped action is a step of the sequence you are then judged on.
            SayWhat.SayWhatDirector d = SayWhat.SayWhatDirector.Instance;
            if (d == null) return;
            if (action == 0 || action > (byte)SayWhat.PartyAction.Bow) return;   // never trust a client
            d.SubmitAction(this, (SayWhat.PartyAction)action);
        }

        /// <summary>Bots have no connection, so the server polls their actions directly.</summary>
        [Server]
        void ServerPollBotActions()
        {
            if (!isBot || _actions == null) return;
            SayWhat.SayWhatDirector d = SayWhat.SayWhatDirector.Instance;
            if (d == null) return;

            SayWhat.PartyAction a = _actions.Poll();
            if (a != SayWhat.PartyAction.None) d.SubmitAction(this, a);
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

            ServerPollBotActions();

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
            if (_filament != null)
            {
                var d = RedLight.RedLightDirector.Instance;
                _filament.mood =
                    eliminated ? Character.FilamentMood.Broken :
                    finished   ? Character.FilamentMood.Smug   :
                    d == null  ? Character.FilamentMood.Idle   :
                    d.MustFreeze ? Character.FilamentMood.Frozen :
                    d.phase == RedLight.RoundPhase.Go ? Character.FilamentMood.Running :
                    Character.FilamentMood.Idle;
            }

            // NAME TAGS: small, near, and never your own.
            //
            // The old behaviour drew every tag at full size all the time. From the wide
            // broadcast shot that put five of them on top of one another in the middle of
            // the screen as one illegible pile - it was the single worst thing in the
            // captured frame. Fall Guys does show names, so they stay, but only for
            // players close enough to matter and faded out with distance. Your own is
            // hidden: the camera is already behind you, so a tag over your head is a
            // label saying "you are here".
            if (nameTag == null) return;
            Camera c = Camera.main;
            if (c == null) return;

            nameTag.transform.rotation = c.transform.rotation;

            if (isLocalPlayer) { nameTag.gameObject.SetActive(false); return; }

            float dist = Vector3.Distance(c.transform.position, transform.position);
            bool near = dist < nameFadeEnd;
            if (nameTag.gameObject.activeSelf != near) nameTag.gameObject.SetActive(near);
            if (!near) return;

            nameTag.transform.localScale =
                Vector3.one * Mathf.Clamp(dist * nameSizePerMetre, 0.10f, 0.70f);

            float a = Mathf.InverseLerp(nameFadeEnd, nameFadeStart, dist);
            Color baseCol = eliminated ? new Color(0.6f, 0.6f, 0.65f) : colour;
            nameTag.color = new Color(baseCol.r, baseCol.g, baseCol.b, a);
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
            // The chassis colour comes from the look now; elimination greys it out.
            if (eliminated && bodyRenderer != null)
                bodyRenderer.material.SetColor("_BaseColor", new Color(0.32f, 0.32f, 0.36f));
            // NOTE: alpha is owned by the distance fade in LateUpdate, so only the RGB
            // is set here. Writing an opaque colour caused tags to pop back to full
            // strength for a frame every time someone was eliminated.
            if (nameTag != null)
            {
                Color c = eliminated ? new Color(0.6f, 0.6f, 0.65f) : colour;
                nameTag.color = new Color(c.r, c.g, c.b, nameTag.color.a);
            }
        }
    }
}
