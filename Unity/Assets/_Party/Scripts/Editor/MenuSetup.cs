using Party.Character;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Party.EditorTools
{
    /// <summary>
    /// Builds the PARTY GAME front end: podium, live Filament, and five panels.
    ///
    ///     Unity -batchmode -quit -executeMethod Party.EditorTools.MenuSetup.Build
    ///
    /// Built from code so the whole screen is reproducible and reviewable in a diff,
    /// rather than assembled by dragging in the inspector.
    /// </summary>
    public static class MenuSetup
    {
        const string ScenePath = "Assets/_Party/Scenes/Menu.unity";
        static Font _font;
        static Font _display;
        static MainMenu _menu;
        static Sprite _bubble;

        /// <summary>Nav buttons cycle these so the column reads as colourful bubbles
        /// rather than a stack of grey boxes.</summary>
        static readonly Color[] BubbleCols =
        {
            new Color(0.13f, 0.62f, 0.94f),   // deep sky
            new Color(0.45f, 0.32f, 0.90f),   // indigo
            new Color(0.06f, 0.72f, 0.58f),   // jade
            new Color(0.97f, 0.52f, 0.10f),   // tangerine
            new Color(0.90f, 0.24f, 0.52f),   // magenta
        };
        static int _bubbleIndex;

        static readonly Color Ink    = new Color(0.96f, 0.96f, 0.98f);
        static readonly Color Dim    = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color Accent = new Color(0.98f, 0.78f, 0.25f);
        static readonly Color Play   = new Color(0.95f, 0.12f, 0.38f);
        static readonly Color Slate  = new Color(1f, 1f, 1f, 0.10f);

        [MenuItem("Party/Rebuild menu scene")]
        public static void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                 ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            // Chunky display face for the logo. Arial is why the title read as a label
            // rather than a logo. Luckiest Guy is Apache 2.0 - free for commercial use.
            _display = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Party/Art/Fonts/LuckiestGuy.ttf")
                    ?? _font;
            _bubble = GradientTextures.Bubble("BubbleButton");
            _bubbleIndex = 0;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- set ----------
            GameObject key = new GameObject("Key Light");
            key.AddComponent<Light>().type = LightType.Directional;
            PresentationSetup.Lighting(key);
            key.transform.rotation = Quaternion.Euler(28f, 150f, 0f);
            PresentationSetup.GlobalVolume();

            BuildStage();

            // No floor. The references are pure sky - a ground plane immediately
            // reintroduces the horizon band that made the last version look boxed in.

            GameObject podium = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            podium.name = "Podium";
            podium.transform.position = new Vector3(0f, -1.45f, 0f);
            podium.transform.localScale = new Vector3(1.45f, 0.22f, 1.45f);
            podium.GetComponent<Renderer>().sharedMaterial =
                PresentationSetup.Lit("Podium", Play, 0.5f, 0f, new Color(0.32f, 0.05f, 0.14f));

            // Glowing ring around the podium.
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "PodiumRing";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.position = new Vector3(0f, -1.60f, 0f);
            ring.transform.localScale = new Vector3(1.78f, 0.015f, 1.78f);
            ring.GetComponent<Renderer>().sharedMaterial =
                PresentationSetup.Lit("PodiumRing", new Color(1f, 0.85f, 0.35f), 0.8f, 0f,
                                      new Color(0.95f, 0.70f, 0.20f));

            GameObject charGo = new GameObject("Character");
            charGo.transform.position = new Vector3(0f, -0.5f, 0f);
            charGo.transform.localScale = Vector3.one * 1.5f;
            CharacterDisplay display = charGo.AddComponent<CharacterDisplay>();

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 30f;
            // Pulled back and offset so the whole Filament reads, sitting left of the
            // button column rather than being cropped by it.
            camGo.transform.position = new Vector3(-2.15f, 0.75f, -8.6f);
            camGo.transform.rotation = Quaternion.Euler(4.5f, 11f, 0f);
            camGo.AddComponent<AudioListener>();
            var cd = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (cd == null) cd = camGo.AddComponent<UniversalAdditionalCameraData>();
            cd.renderPostProcessing = true;
            cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            GameObject sys = new GameObject("Systems");
            sys.AddComponent<SteamBoot>();
            sys.AddComponent<MenuAudio>();

            // Rim light from behind so the glass dome separates from the sunburst - a
            // transparent head against a bright backdrop otherwise disappears into it.
            GameObject rim = new GameObject("Rim Light");
            rim.transform.position = new Vector3(-2.6f, 2.2f, 3.4f);
            rim.transform.LookAt(new Vector3(0f, -0.2f, 0f));
            Light rl = rim.AddComponent<Light>();
            rl.type = LightType.Spot; rl.range = 18f; rl.spotAngle = 55f;
            rl.intensity = 9f; rl.color = new Color(0.75f, 0.85f, 1f); rl.shadows = LightShadows.None;   // so the menu can report Steam's real state

            // ---------- canvas ----------
            GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform));
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler cs = canvasGo.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // StandaloneInputModule reads UnityEngine.Input, which THROWS when the project
            // is set to the Input System package - the EventSystem dies and not one button
            // responds. InputSystemUIInputModule is the Input System equivalent.
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            _menu = canvasGo.AddComponent<MainMenu>();
            _menu.display = display;

            // A dark scrim behind the UI column. Without it, white text sits on top of
            // drifting confetti and a rotating sunburst and becomes unreadable - the
            // background being lively is exactly why the foreground needs a backing.
            GameObject scrim = UI("Scrim", canvasGo.transform);
            Place(scrim, TR, new Vector2(0f, 0f), new Vector2(640f, 1080f));
            RectTransform srt = scrim.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(1f, 0f); srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 0.5f);
            srt.anchoredPosition = new Vector2(-40f, 0f);
            srt.sizeDelta = new Vector2(560f, -140f);   // inset panel, not a full-height slab
            Image si = scrim.AddComponent<Image>();
            si.sprite = _bubble;
            si.type = Image.Type.Sliced;
            si.pixelsPerUnitMultiplier = 0.9f;
            si.color = new Color(0.08f, 0.24f, 0.50f, 0.55f);   // deeper glass so bubbles pop
            si.raycastTarget = false;
            GameObject edge = UI("ScrimEdge", canvasGo.transform);
            RectTransform ert = edge.GetComponent<RectTransform>();
            ert.anchorMin = new Vector2(1f, 0f); ert.anchorMax = new Vector2(1f, 1f);
            ert.pivot = new Vector2(1f, 0.5f);
            ert.anchoredPosition = new Vector2(-40f, 0f);
            ert.sizeDelta = new Vector2(572f, -128f);
            Image ei = edge.AddComponent<Image>();
            ei.sprite = _bubble; ei.type = Image.Type.Sliced;
            ei.pixelsPerUnitMultiplier = 0.9f;
            ei.color = new Color(1f, 1f, 1f, 0.20f);
            ei.raycastTarget = false;
            edge.transform.SetSiblingIndex(scrim.transform.GetSiblingIndex());

            // Stacked outlines fake a thick keyline. uGUI's Outline draws four offset
            // copies, so several at increasing distance build the chunky border the
            // reference logos all share.
            Text title = Label(canvasGo.transform, "Title", "PARTY GAME", 108, TL,
                               new Vector2(58f, -44f), new Vector2(1000f, 150f),
                               TextAnchor.UpperLeft, new Color(1f, 0.97f, 0.90f));
            title.font = _display;
            title.transform.localRotation = Quaternion.Euler(0f, 0f, 1.6f);   // slight tilt
            foreach (float d in new[] { 7f, 5f, 3f })
            {
                var o = title.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.14f, 0.06f, 0.22f, 1f);
                o.effectDistance = new Vector2(d, -d);
                o.useGraphicAlpha = false;
            }
            var tSh = title.gameObject.AddComponent<Shadow>();
            tSh.effectColor = new Color(0.55f, 0.15f, 0.35f, 0.9f);
            tSh.effectDistance = new Vector2(0f, -12f);

            Text sub = Label(canvasGo.transform, "Sub", "working title", 26, TL,
                             new Vector2(72f, -168f), new Vector2(700f, 40f),
                             TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.8f));
            sub.font = _display;
            var sOut = sub.gameObject.AddComponent<Outline>();
            sOut.effectColor = new Color(0.14f, 0.06f, 0.22f, 0.9f);
            sOut.effectDistance = new Vector2(2f, -2f);

            _menu.homePanel        = BuildHome(canvasGo.transform);
            _menu.characterPanel   = BuildCharacter(canvasGo.transform);
            _menu.settingsPanel    = BuildSettings(canvasGo.transform);
            _menu.controlsPanel    = BuildControls(canvasGo.transform);
            _menu.multiplayerPanel = BuildMultiplayer(canvasGo.transform);

            System.IO.Directory.CreateDirectory("Assets/_Party/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene("Assets/_Party/Scenes/RedLight.unity", true),
                new EditorBuildSettingsScene("Assets/_Party/Scenes/NetTest.unity", true),
            };
            Debug.Log("[Party] menu scene built.");
        }

        /// <summary>
        /// Studio set: a radial sunburst backdrop, a curved wall, spotlights and confetti.
        /// The game is a televised show, so the menu should look like a stage with the
        /// lights on rather than an empty skybox.
        /// </summary>
        static void BuildStage()
        {
            GameObject stage = new GameObject("Stage");
            MenuStage ms = stage.AddComponent<MenuStage>();

            // RICHER SKY. The first pass was so pale it read as washed out - a flat
            // near-white blue with nothing in it. Deeper top, warm horizon, so there is
            // actual range in the frame.
            Texture2D sky = GradientTextures.Vertical(
                "SkyGradient",
                new Color(0.16f, 0.52f, 0.92f),   // deep blue overhead
                new Color(0.99f, 0.82f, 0.62f),   // warm light at the horizon
                256);
            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(back.GetComponent<Collider>());
            back.name = "Sky";
            back.transform.SetParent(stage.transform, false);
            back.transform.position = new Vector3(0f, 1f, 26f);
            back.transform.localScale = new Vector3(140f, 80f, 1f);
            back.GetComponent<Renderer>().sharedMaterial =
                GradientTextures.UnlitTex("SkyMat", sky, Color.white);

            BuildGameProps(stage.transform);

            Texture2D puff = GradientTextures.Bokeh("CloudPuff", 256);
            ms.bokehMaterials = new[]
            {
                GradientTextures.UnlitTex("Cloud0", puff, new Color(1f, 1f, 1f, 0.80f)),
                GradientTextures.UnlitTex("Cloud1", puff, new Color(1f, 0.96f, 0.90f, 0.60f)),
                GradientTextures.UnlitTex("Balloon0", puff, new Color(1f, 0.42f, 0.55f, 0.55f)),
                GradientTextures.UnlitTex("Balloon1", puff, new Color(1f, 0.82f, 0.30f, 0.55f)),
            };
            ms.confettiCount = 20;
            ms.area = new Vector3(30f, 16f, 12f);
            ms.driftSideways = true;

            var lights = new List<Light>();
            GameObject sun = new GameObject("Sun");
            sun.transform.SetParent(stage.transform, false);
            sun.transform.rotation = Quaternion.Euler(38f, 24f, 0f);
            Light sl = sun.AddComponent<Light>();
            sl.type = LightType.Directional; sl.intensity = 2.1f;
            sl.color = new Color(1f, 0.95f, 0.86f); sl.shadows = LightShadows.Soft;
            lights.Add(sl);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.55f, 0.74f, 1.00f);
            RenderSettings.ambientEquatorColor = new Color(0.86f, 0.82f, 0.80f);
            RenderSettings.ambientGroundColor  = new Color(0.50f, 0.45f, 0.48f);
            RenderSettings.fog = false;

            ms.pulseLights = lights.ToArray();
            ms.pulseAmount = 0.04f; ms.pulseSpeed = 0.5f;
        }

        /// <summary>
        /// THE GAME'S OWN FURNITURE, behind the contestant.
        ///
        /// An empty sky says nothing about what this game is. These are the actual props
        /// from Red Light - the gold finish gantry, red piston blocks, slalom pillars,
        /// a stretch of lane - plus bunting and a few other Filaments waiting their turn.
        /// Depth of field softens them, so they read as a place rather than clutter.
        /// </summary>
        static void BuildGameProps(Transform parent)
        {
            GameObject props = new GameObject("GameProps");
            props.transform.SetParent(parent, false);

            Material gold    = PresentationSetup.Lit("MenuGold", new Color(0.99f, 0.80f, 0.24f), 0.75f, 0.55f, new Color(0.45f, 0.32f, 0.04f));
            Material hazard  = PresentationSetup.Lit("MenuHazard", new Color(0.92f, 0.30f, 0.32f), 0.4f);
            Material pillar  = PresentationSetup.Lit("MenuPillar", new Color(0.86f, 0.88f, 0.94f), 0.35f);
            Material laneMat = PresentationSetup.Lit("MenuLane", new Color(0.42f, 0.46f, 0.58f), 0.2f);

            // A floating stretch of lane the podium sits on.
            GameObject lane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(lane.GetComponent<Collider>());
            lane.name = "LaneSlab";
            lane.transform.SetParent(props.transform, false);
            lane.transform.position = new Vector3(0f, -2.1f, 3.5f);
            lane.transform.localScale = new Vector3(9f, 0.7f, 22f);
            lane.GetComponent<Renderer>().sharedMaterial = laneMat;

            // Finish gantry down the lane.
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(post.GetComponent<Collider>());
                post.transform.SetParent(props.transform, false);
                post.transform.position = new Vector3(sgn * 3.6f, 0.2f, 13f);
                post.transform.localScale = new Vector3(0.45f, 4.6f, 0.45f);
                post.GetComponent<Renderer>().sharedMaterial = gold;
            }
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.transform.SetParent(props.transform, false);
            beam.transform.position = new Vector3(0f, 2.4f, 13f);
            beam.transform.localScale = new Vector3(7.8f, 0.55f, 0.45f);
            beam.GetComponent<Renderer>().sharedMaterial = gold;

            // Hazards along the lane.
            float[] zs = { 6.5f, 9.5f };
            for (int i = 0; i < zs.Length; i++)
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    GameObject blk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(blk.GetComponent<Collider>());
                    blk.transform.SetParent(props.transform, false);
                    blk.transform.position = new Vector3(sgn * 2.6f, -1.2f, zs[i]);
                    blk.transform.localScale = new Vector3(1.9f, 1.2f, 1.1f);
                    blk.GetComponent<Renderer>().sharedMaterial = hazard;
                }
            for (int k = -1; k <= 1; k += 2)
            {
                GameObject pil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(pil.GetComponent<Collider>());
                pil.transform.SetParent(props.transform, false);
                pil.transform.position = new Vector3(k * 1.6f, -0.9f, 8f);
                pil.transform.localScale = new Vector3(1.1f, 0.9f, 1.1f);
                pil.GetComponent<Renderer>().sharedMaterial = pillar;
            }

            // Bunting strung across the frame - instantly reads "party".
            Color[] flagCols =
            {
                new Color(0.98f,0.35f,0.45f), new Color(0.35f,0.72f,0.98f),
                new Color(0.35f,0.86f,0.55f), new Color(1f,0.82f,0.28f),
                new Color(0.72f,0.48f,0.95f),
            };
            for (int i = 0; i < 16; i++)
            {
                GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(flag.GetComponent<Collider>());
                flag.name = "Flag" + i;
                flag.transform.SetParent(props.transform, false);
                float t = i / 15f;
                float x = Mathf.Lerp(-13f, 13f, t);
                float y = 5.6f - Mathf.Sin(t * Mathf.PI) * 1.5f;      // gentle sag
                flag.transform.position = new Vector3(x, y, 11f);
                flag.transform.localScale = new Vector3(0.85f, 1.15f, 1f);
                flag.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                flag.GetComponent<Renderer>().sharedMaterial =
                    PresentationSetup.Lit("Flag" + i, flagCols[i % flagCols.Length], 0.25f);
            }

            // Other contestants waiting - the same Filament, so the menu shows the cast.
            int[] chassis = { 1, 3, 4, 6 };
            for (int i = 0; i < 4; i++)
            {
                GameObject other = new GameObject("Contestant" + i);
                other.transform.SetParent(props.transform, false);
                other.transform.position = new Vector3(-6.5f + i * 4.3f, -1.15f, 8.5f + (i % 2) * 1.6f);
                other.transform.localScale = Vector3.one * 0.85f;
                other.transform.localRotation = Quaternion.Euler(0f, -18f + i * 12f, 0f);
                CharacterLook.Build(other.transform, new LookConfig
                {
                    chassis = chassis[i], livery = i % 5, filament = i % 6,
                    shape = i % 5, dome = 0, mask = 0, accessory = (i * 2) % 5,
                }, out _, out _);
            }
        }

        // ---------- panels ----------

        static GameObject BuildHome(Transform parent)
        {
            GameObject p = Panel(parent, "HomePanel");
            float y = -250f;
            BigButton(p.transform, "PLAY", y, Play, 46, nameof(MainMenu.PlayLocal)); y -= 116f;
            NavButton(p.transform, "CHARACTER",   y, (int)MainMenu.Panel.Character);   y -= 84f;
            NavButton(p.transform, "MULTIPLAYER", y, (int)MainMenu.Panel.Multiplayer); y -= 84f;
            NavButton(p.transform, "SETTINGS",    y, (int)MainMenu.Panel.Settings);    y -= 84f;
            NavButton(p.transform, "CONTROLS",    y, (int)MainMenu.Panel.Controls);    y -= 84f;
            VoidButton(p.transform, "QUIT", TR, new Vector2(-64f, y), new Vector2(430f, 62f),
                       new Color(0.28f, 0.28f, 0.33f), 24, nameof(MainMenu.Quit));
            return p;
        }

        static GameObject BuildCharacter(Transform parent)
        {
            GameObject p = Panel(parent, "CharacterPanel");
            Label(p.transform, "H", "YOUR FILAMENT", 34, TR, new Vector2(-64f, -240f),
                  new Vector2(560f, 44f), TextAnchor.UpperRight, Accent);
            Label(p.transform, "Blurb", "the glow shows how the host feels about you",
                  19, TR, new Vector2(-64f, -284f), new Vector2(560f, 30f), TextAnchor.UpperRight, Dim);

            _menu.nameField = Field(p.transform, "Name", "your name", TR, new Vector2(-64f, -324f), new Vector2(560f, 56f));

            float y = -396f;
            _menu.lookValues = new Text[MainMenu.RowCount];
            for (int i = 0; i < MainMenu.RowCount; i++)
            {
                _menu.lookValues[i] = LookRow(p.transform, MainMenu.RowCaption(i), i, y);
                y -= 62f;
            }
            VoidButton(p.transform, "RANDOMISE", TR, new Vector2(-64f, y - 6f), new Vector2(270f, 54f),
                       new Color(0.35f, 0.30f, 0.55f), 22, nameof(MainMenu.Randomise));
            Back(p.transform, y - 6f, -300f);
            return p;
        }

        static GameObject BuildSettings(Transform parent)
        {
            GameObject p = Panel(parent, "SettingsPanel");
            Label(p.transform, "H", "SETTINGS", 34, TR, new Vector2(-64f, -240f),
                  new Vector2(560f, 44f), TextAnchor.UpperRight, Accent);
            float y = -310f;
            _menu.volumeLabel      = SettingRow(p.transform, "Volume",      y, nameof(MainMenu.StepVolume));       y -= 70f;
            _menu.qualityLabel     = SettingRow(p.transform, "Quality",     y, nameof(MainMenu.StepQuality));      y -= 70f;
            _menu.fullscreenLabel  = SettingRow(p.transform, "Fullscreen",  y, nameof(MainMenu.ToggleFullscreen)); y -= 70f;
            _menu.participantsLabel= SettingRow(p.transform, "Players",     y, nameof(MainMenu.StepParticipants)); y -= 70f;
            _menu.hostVoiceLabel   = SettingRow(p.transform, "AI host",     y, nameof(MainMenu.ToggleHostVoice));  y -= 70f;
            Label(p.transform, "HostNote",
                  "Turn the host off to check the game still works without him.",
                  17, TR, new Vector2(-64f, y), new Vector2(560f, 50f), TextAnchor.UpperRight, Dim);
            Back(p.transform, y - 60f, 0f);
            return p;
        }

        static GameObject BuildControls(Transform parent)
        {
            GameObject p = Panel(parent, "ControlsPanel");
            Label(p.transform, "H", "CONTROLS", 34, TR, new Vector2(-64f, -240f),
                  new Vector2(560f, 44f), TextAnchor.UpperRight, Accent);
            string body =
                "MOVE\n" +
                "   W A S D   or   Arrow Keys\n" +
                "   Left stick on a gamepad\n\n" +
                "THAT IS THE WHOLE CONTROL SCHEME.\n" +
                "Two action buttons maximum is a hard rule,\n" +
                "and Red Light does not need either of them.\n\n" +
                "Move on GO. Freeze on STOP.\n" +
                "Barnaby is biased and he lies.";
            Label(p.transform, "Body", body, 22, TR, new Vector2(-64f, -300f),
                  new Vector2(560f, 420f), TextAnchor.UpperRight, Ink);
            Back(p.transform, -740f, 0f);
            return p;
        }

        static GameObject BuildMultiplayer(Transform parent)
        {
            GameObject p = Panel(parent, "MultiplayerPanel");
            Label(p.transform, "H", "MULTIPLAYER", 34, TR, new Vector2(-64f, -240f),
                  new Vector2(560f, 44f), TextAnchor.UpperRight, Accent);

            _menu.steamHostButton = VoidButton(p.transform, "HOST ON STEAM", TR, new Vector2(-64f, -300f),
                                               new Vector2(560f, 74f), new Color(0.20f, 0.45f, 0.75f), 26,
                                               nameof(MainMenu.HostOnSteam));
            Label(p.transform, "CodeNote", "you get a 5-letter join code to share", 17, TR,
                  new Vector2(-64f, -380f), new Vector2(560f, 28f), TextAnchor.UpperRight, Dim);

            _menu.joinCodeField = Field(p.transform, "JoinCode", "JOIN CODE", TR,
                                        new Vector2(-244f, -416f), new Vector2(380f, 60f));
            _menu.steamJoinButton = VoidButton(p.transform, "JOIN", TR, new Vector2(-64f, -416f),
                                               new Vector2(168f, 60f), new Color(0.25f, 0.55f, 0.40f), 24,
                                               nameof(MainMenu.JoinByCode));

            _menu.joinAddressField = Field(p.transform, "JoinAddr", "localhost", TR,
                                           new Vector2(-244f, -492f), new Vector2(380f, 56f));
            VoidButton(p.transform, "DIRECT", TR, new Vector2(-64f, -492f), new Vector2(168f, 56f),
                       new Color(0.33f, 0.33f, 0.40f), 22, nameof(MainMenu.JoinByAddress));

            _menu.steamLabel = Label(p.transform, "SteamStatus", "", 18, TR, new Vector2(-64f, -566f),
                                     new Vector2(560f, 70f), TextAnchor.UpperRight, new Color(1f, 0.6f, 0.55f));
            Back(p.transform, -650f, 0f);
            return p;
        }

        // ---------- widgets ----------

        static readonly Vector2 TL = new Vector2(0f, 1f);
        static readonly Vector2 TR = new Vector2(1f, 1f);

        /// <summary>A UI GameObject is created WITH its RectTransform - adding one later
        /// to an object that already has a Transform is the usual source of grief.</summary>
        static GameObject UI(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject Panel(Transform parent, string name)
        {
            GameObject go = UI(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }

        static RectTransform Place(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            // NOT '??'. Unity overloads == for its objects but ?? uses real null, so a
            // missing component can slip through the coalesce and blow up later. Every UI
            // object here is created WITH a RectTransform (see UI()), and this is the belt
            // and braces.
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        static Text Label(Transform parent, string name, string text, int size, Vector2 anchor,
                          Vector2 pos, Vector2 sizeD, TextAnchor align, Color colour)
        {
            GameObject go = UI(name, parent);
            Place(go, anchor, pos, sizeD);
            Text t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.text = text; t.alignment = align; t.color = colour;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static Button Shell(Transform parent, string name, string text, Vector2 anchor, Vector2 pos,
                            Vector2 size, Color colour, int fontSize)
        {
            GameObject go = UI(name, parent);
            Place(go, anchor, pos, size);
            Image img = go.AddComponent<Image>();
            img.sprite = _bubble;
            img.type = Image.Type.Sliced;      // 9-slice: corners stay round at any size
            img.pixelsPerUnitMultiplier = 1.6f;
            img.color = colour;
            Button b = go.AddComponent<Button>();
            b.targetGraphic = img;
            ColorBlock cb = b.colors;
            cb.highlightedColor = Color.Lerp(colour, Color.white, 0.22f);
            cb.pressedColor     = Color.Lerp(colour, Color.black, 0.22f);
            cb.disabledColor    = new Color(colour.r, colour.g, colour.b, 0.22f);
            b.colors = cb;
            Text t = Label(go.transform, "Text", text, fontSize, new Vector2(0.5f, 0.5f),
                           Vector2.zero, size, TextAnchor.MiddleCenter, Color.white);
            Place(t.gameObject, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            t.font = _display;
            var bo = t.gameObject.AddComponent<Outline>();
            bo.effectColor = new Color(0.12f, 0.06f, 0.18f, 0.85f);
            bo.effectDistance = new Vector2(2f, -2f);
            go.AddComponent<ButtonFeel>();
            return b;
        }

        static Button VoidButton(Transform parent, string text, Vector2 anchor, Vector2 pos,
                                 Vector2 size, Color colour, int fontSize, string method)
        {
            Button b = Shell(parent, text, text, anchor, pos, size, colour, fontSize);
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
                b.onClick, (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
                    typeof(UnityEngine.Events.UnityAction), _menu, method));
            return b;
        }

        static Button IntButton(Transform parent, string name, string text, Vector2 anchor, Vector2 pos,
                                Vector2 size, Color colour, int fontSize, string method, int arg)
        {
            Button b = Shell(parent, name, text, anchor, pos, size, colour, fontSize);
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(
                b.onClick, (UnityEngine.Events.UnityAction<int>)System.Delegate.CreateDelegate(
                    typeof(UnityEngine.Events.UnityAction<int>), _menu, method), arg);
            return b;
        }

        static void BigButton(Transform parent, string text, float y, Color c, int size, string method)
            => VoidButton(parent, text, TR, new Vector2(-64f, y), new Vector2(430f, 96f), c, size, method);

        static void NavButton(Transform parent, string text, float y, int panel)
            => IntButton(parent, text, text, TR, new Vector2(-64f, y), new Vector2(430f, 72f),
                         BubbleCols[_bubbleIndex++ % BubbleCols.Length], 26, nameof(MainMenu.Show), panel);

        static void Back(Transform parent, float y, float extraX)
            => IntButton(parent, "Back", "BACK", TR, new Vector2(-64f + extraX, y), new Vector2(200f, 54f),
                         new Color(1f, 1f, 1f, 0.12f), 22, nameof(MainMenu.Show), (int)MainMenu.Panel.Home);

        static InputField Field(Transform parent, string name, string placeholder, Vector2 anchor,
                                Vector2 pos, Vector2 size)
        {
            GameObject go = UI(name, parent);
            Place(go, anchor, pos, size);
            Image bg = go.AddComponent<Image>();
            bg.sprite = _bubble;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 2.2f;
            bg.color = new Color(1f, 1f, 1f, 0.16f);
            Text text = Label(go.transform, "Text", "", 24, new Vector2(0.5f, 0.5f), Vector2.zero,
                              size - new Vector2(28f, 10f), TextAnchor.MiddleLeft, Ink);
            text.raycastTarget = true;
            Text ph = Label(go.transform, "Placeholder", placeholder, 24, new Vector2(0.5f, 0.5f),
                            Vector2.zero, size - new Vector2(28f, 10f), TextAnchor.MiddleLeft, Dim);
            InputField f = go.AddComponent<InputField>();
            f.textComponent = text; f.placeholder = ph; f.targetGraphic = bg;
            return f;
        }

        /// <summary>"Caption   [&lt;]  value  [&gt;]" for one customisation row.</summary>
        static Text LookRow(Transform parent, string caption, int row, float y)
        {
            Label(parent, caption + "Cap", caption, 18, TR, new Vector2(-64f, y),
                  new Vector2(560f, 24f), TextAnchor.UpperRight, Dim);
            IntButton(parent, caption + "-", "<", TR, new Vector2(-490f, y - 24f), new Vector2(54f, 46f),
                      new Color(0.42f, 0.55f, 0.85f), 24, nameof(MainMenu.StepLook), row * 10 + 0);
            Text v = Label(parent, caption + "Val", "-", 26, TR, new Vector2(-124f, y - 24f),
                           new Vector2(360f, 44f), TextAnchor.MiddleCenter, Ink);
            IntButton(parent, caption + "+", ">", TR, new Vector2(-64f, y - 24f), new Vector2(54f, 46f),
                      new Color(0.42f, 0.55f, 0.85f), 24, nameof(MainMenu.StepLook), row * 10 + 1);
            return v;
        }

        static Text SettingRow(Transform parent, string caption, float y, string method)
        {
            Label(parent, caption + "Cap", caption, 20, TR, new Vector2(-64f, y),
                  new Vector2(560f, 26f), TextAnchor.UpperRight, Dim);
            IntButton(parent, caption + "-", "<", TR, new Vector2(-490f, y - 26f), new Vector2(54f, 46f),
                      new Color(0.42f, 0.55f, 0.85f), 24, method, -1);
            Text v = Label(parent, caption + "Val", "-", 24, TR, new Vector2(-124f, y - 26f),
                           new Vector2(360f, 44f), TextAnchor.MiddleCenter, Ink);
            IntButton(parent, caption + "+", ">", TR, new Vector2(-64f, y - 26f), new Vector2(54f, 46f),
                      new Color(0.42f, 0.55f, 0.85f), 24, method, 1);
            return v;
        }
    }
}
