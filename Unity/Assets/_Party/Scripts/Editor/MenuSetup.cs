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
        static readonly Color Play   = new Color(0.93f, 0.20f, 0.42f);
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
            podium.transform.position = new Vector3(0f, -2.25f, 0f);
            podium.transform.localScale = new Vector3(2.1f, 0.30f, 2.1f);
            podium.GetComponent<Renderer>().sharedMaterial =
                PresentationSetup.Lit("Podium", Play, 0.5f, 0f, new Color(0.32f, 0.05f, 0.14f));

            // Glowing ring around the podium.
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "PodiumRing";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.position = new Vector3(0f, -2.44f, 0f);
            ring.transform.localScale = new Vector3(2.55f, 0.02f, 2.55f);
            ring.GetComponent<Renderer>().sharedMaterial =
                PresentationSetup.Lit("PodiumRing", new Color(1f, 0.85f, 0.35f), 0.8f, 0f,
                                      new Color(0.95f, 0.70f, 0.20f));

            GameObject charGo = new GameObject("Character");
            charGo.transform.position = new Vector3(0f, -0.75f, 0f);
            charGo.transform.localScale = Vector3.one * 2.35f;   // hero scale
            CharacterDisplay display = charGo.AddComponent<CharacterDisplay>();

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 30f;
            // Pulled back and offset so the whole Filament reads, sitting left of the
            // button column rather than being cropped by it.
            // Low and close, looking slightly UP at the subject - the standard poster
            // angle, and it puts the hero against sky rather than against clutter.
            camGo.transform.position = new Vector3(-2.05f, -0.15f, -6.4f);
            camGo.transform.rotation = Quaternion.Euler(-3.5f, 11f, 0f);
            camGo.AddComponent<AudioListener>();
            var cd = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (cd == null) cd = camGo.AddComponent<UniversalAdditionalCameraData>();
            cd.renderPostProcessing = true;
            cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            GameObject sys = new GameObject("Systems");
            sys.AddComponent<SteamBoot>();
            sys.AddComponent<MenuAudio>();

            // Cinematic key + rim. A poster is lit, not evenly exposed: a strong warm key
            // from one side and a hard cool rim from behind is what separates a subject
            // from its background.
            GameObject keyGo = new GameObject("Key");
            keyGo.transform.position = new Vector3(3.4f, 3.0f, -3.2f);
            keyGo.transform.LookAt(new Vector3(0f, -0.3f, 0f));
            Light keyL = keyGo.AddComponent<Light>();
            keyL.type = LightType.Spot; keyL.range = 26f; keyL.spotAngle = 58f;
            keyL.intensity = 22f; keyL.color = new Color(1f, 0.92f, 0.80f);
            keyL.shadows = LightShadows.Soft;

            GameObject rimGo = new GameObject("Rim");
            rimGo.transform.position = new Vector3(-3.0f, 2.6f, 4.2f);
            rimGo.transform.LookAt(new Vector3(0f, -0.2f, 0f));
            Light rimL = rimGo.AddComponent<Light>();
            rimL.type = LightType.Spot; rimL.range = 22f; rimL.spotAngle = 50f;
            rimL.intensity = 26f; rimL.color = new Color(0.62f, 0.80f, 1f);
            rimL.shadows = LightShadows.None;

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

            // Everything breathes: slow camera sway, parallax between layers, swaying
            // bunting, sweeping rays. A still frame with one spinning object reads as a
            // turntable; small motion everywhere reads as a living poster.
            PosterMotion poster = sys.AddComponent<PosterMotion>();
            poster.cam = camGo.transform;

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
            si.color = new Color(0.08f, 0.24f, 0.50f, 0.52f);   // deeper glass so bubbles pop
            si.raycastTarget = false;

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

            // THE ARENA, built to match Docs/ArtTarget/menu_target.svg.
            //
            // Two earlier attempts failed in opposite directions: props large and near
            // camera (cluttered, buried the sky), then faint hazy hints (read as nothing).
            // The reference art does neither - a fully readable environment, kept BEHIND
            // the hero and softened by depth of field so the eye still lands on the
            // character first.
            Texture2D sky = GradientTextures.Vertical("SkyGradient",
                new Color(0.05f, 0.31f, 0.72f), new Color(1.00f, 0.88f, 0.69f), 256);
            GameObject skyQ = Quad(stage.transform, "Sky", new Vector3(0f, 6f, 74f),
                                   new Vector3(220f, 120f, 1f));
            skyQ.GetComponent<Renderer>().sharedMaterial =
                GradientTextures.UnlitTex("SkyMat", sky, Color.white);

            Material floorM  = PresentationSetup.Lit("ArenaFloor", new Color(0.36f, 0.42f, 0.55f), 0.15f);
            Material laneM   = PresentationSetup.Lit("ArenaLane",  new Color(0.56f, 0.64f, 0.80f), 0.18f);
            Material standM  = PresentationSetup.Lit("ArenaStand", new Color(0.20f, 0.24f, 0.38f), 0.12f);
            Material goldM   = PresentationSetup.Lit("ArenaGold",  new Color(0.96f, 0.80f, 0.36f), 0.7f, 0.5f, new Color(0.40f, 0.28f, 0.04f));
            Material hazardM = PresentationSetup.Lit("ArenaHazard",new Color(0.91f, 0.33f, 0.37f), 0.35f);
            Material paleM   = PresentationSetup.Lit("ArenaPale",  new Color(0.88f, 0.91f, 0.96f), 0.3f);
            Material trussM  = PresentationSetup.Lit("ArenaTruss", new Color(0.17f, 0.20f, 0.31f), 0.4f);

            // arena floor + the lane running back to the finish
            Box(stage.transform, "ArenaFloor", new Vector3(0f, -3.4f, 26f), new Vector3(80f, 0.6f, 74f), floorM);
            Box(stage.transform, "Lane",       new Vector3(0f, -3.05f, 26f), new Vector3(17f, 0.4f, 66f), laneM);
            Box(stage.transform, "StartStripe",new Vector3(0f, -2.82f, 6f),  new Vector3(17f, 0.1f, 1.2f), paleM);

            // stands both sides, tiered, with a coloured crowd
            Color[] crowd = { new Color(1f,0.50f,0.61f), new Color(0.42f,0.78f,0.96f),
                              new Color(0.50f,0.89f,0.70f), new Color(1f,0.84f,0.37f),
                              new Color(0.70f,0.48f,0.95f) };
            for (int side = -1; side <= 1; side += 2)
                for (int tier = 0; tier < 3; tier++)
                {
                    float x = side * (12f + tier * 3.2f);
                    float y = -2.6f + tier * 1.7f;
                    Box(stage.transform, $"Stand{side}{tier}", new Vector3(x, y, 30f),
                        new Vector3(3.0f, 1.6f, 60f), standM);
                    for (int k = 0; k < 9; k++)
                    {
                        GameObject c = Sphere(stage.transform, "Fan", 
                            new Vector3(x + Random.Range(-0.8f, 0.8f), y + 1.2f, 6f + k * 6.5f),
                            Vector3.one * 0.85f);
                        c.GetComponent<Renderer>().sharedMaterial =
                            PresentationSetup.Lit("Crowd" + ((k + tier) % 5), crowd[(k + tier) % 5], 0.2f);
                    }
                }

            // FINISH GANTRY with a banner
            Box(stage.transform, "GantryL", new Vector3(-7.6f, 0.4f, 58f), new Vector3(0.7f, 7.4f, 0.7f), goldM);
            Box(stage.transform, "GantryR", new Vector3( 7.6f, 0.4f, 58f), new Vector3(0.7f, 7.4f, 0.7f), goldM);
            Box(stage.transform, "GantryTop", new Vector3(0f, 4.2f, 58f), new Vector3(16.2f, 1.1f, 0.7f), goldM);
            Box(stage.transform, "Banner",    new Vector3(0f, 2.9f, 57.6f), new Vector3(13.5f, 1.4f, 0.2f), trussM);

            // hazards: sweeper bar with a post, piston blocks, slalom pillars
            Box(stage.transform, "Sweeper", new Vector3(0f, -2.3f, 34f), new Vector3(13f, 0.55f, 0.55f), hazardM);
            Cyl(stage.transform, "SweepPost", new Vector3(0f, -2.5f, 34f), new Vector3(0.6f, 0.7f, 0.6f), paleM);
            Box(stage.transform, "PistonL", new Vector3(-6.2f, -2.4f, 22f), new Vector3(3.4f, 1.5f, 1.6f), hazardM);
            Box(stage.transform, "PistonR", new Vector3( 6.2f, -2.4f, 22f), new Vector3(3.4f, 1.5f, 1.6f), hazardM);
            Cyl(stage.transform, "PillarL", new Vector3(-3.0f, -2.1f, 44f), new Vector3(1.5f, 1.1f, 1.5f), paleM);
            Cyl(stage.transform, "PillarR", new Vector3( 3.0f, -2.1f, 44f), new Vector3(1.5f, 1.1f, 1.5f), paleM);

            // contestants out on the lane, clearly readable
            int[] chassis = { 1, 2, 3 };
            float[] zs = { 20f, 30f, 41f };
            float[] xs = { -4.2f, 3.6f, -1.4f };
            for (int i = 0; i < 3; i++)
            {
                GameObject o = new GameObject("Runner" + i);
                o.transform.SetParent(stage.transform, false);
                o.transform.position = new Vector3(xs[i], -1.9f, zs[i]);
                o.transform.localScale = Vector3.one * 1.15f;
                o.transform.localRotation = Quaternion.Euler(0f, -14f + i * 13f, 0f);
                CharacterLook.Build(o.transform, new LookConfig
                { chassis = chassis[i], livery = i, filament = i, shape = i, dome = 0, mask = 0, accessory = 0 },
                    out _, out _);
            }

            // lighting truss above the lane
            Box(stage.transform, "Truss", new Vector3(0f, 11.5f, 30f), new Vector3(30f, 0.6f, 0.6f), trussM);
            for (int i = -1; i <= 1; i++)
                Box(stage.transform, "Lamp" + i, new Vector3(i * 9f, 10.9f, 30f), new Vector3(1.1f, 0.9f, 1.1f), trussM);

            // bunting across the front, fully in frame
            Color[] flagCols = { new Color(1f,0.37f,0.49f), new Color(0.25f,0.69f,0.96f),
                                 new Color(0.37f,0.88f,0.63f), new Color(1f,0.82f,0.24f),
                                 new Color(0.69f,0.48f,0.94f), new Color(1f,0.56f,0.30f) };
            GameObject bunting = new GameObject("Bunting");
            bunting.transform.SetParent(stage.transform, false);
            for (int i = 0; i < 20; i++)
            {
                float t = i / 19f;
                GameObject f = Quad(bunting.transform, "Flag" + i,
                    new Vector3(Mathf.Lerp(-24f, 24f, t), 8.4f - Mathf.Sin(t * Mathf.PI) * 1.9f, 16f),
                    new Vector3(1.15f, 1.5f, 1f));
                f.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                f.GetComponent<Renderer>().sharedMaterial =
                    PresentationSetup.Lit("Flag" + i, flagCols[i % flagCols.Length], 0.25f);
            }
            bunting.AddComponent<BuntingSway>();

            // drifting confetti - now genuinely transparent
            Texture2D dot = GradientTextures.Bokeh("BokehDot", 128);
            ms.bokehMaterials = new[]
            {
                GradientTextures.UnlitTex("Conf0", dot, new Color(1f, 0.45f, 0.58f, 0.75f)),
                GradientTextures.UnlitTex("Conf1", dot, new Color(0.35f, 0.75f, 1f, 0.70f)),
                GradientTextures.UnlitTex("Conf2", dot, new Color(1f, 0.86f, 0.36f, 0.70f)),
                GradientTextures.UnlitTex("Conf3", dot, new Color(0.50f, 0.92f, 0.70f, 0.65f)),
            };
            ms.confettiCount = 30;
            ms.area = new Vector3(26f, 14f, 10f);
            ms.driftSideways = false;

            // sunny key light
            GameObject sun = new GameObject("Sun");
            sun.transform.SetParent(stage.transform, false);
            sun.transform.rotation = Quaternion.Euler(40f, 22f, 0f);
            Light sl = sun.AddComponent<Light>();
            sl.type = LightType.Directional; sl.intensity = 2.0f;
            sl.color = new Color(1f, 0.95f, 0.86f); sl.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.58f, 0.76f, 1.00f);
            RenderSettings.ambientEquatorColor = new Color(0.84f, 0.84f, 0.88f);
            RenderSettings.ambientGroundColor  = new Color(0.42f, 0.44f, 0.52f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.85f, 0.97f);
            RenderSettings.fogStartDistance = 34f;
            RenderSettings.fogEndDistance = 96f;

            ms.pulseLights = new[] { sl };
            ms.pulseAmount = 0.04f; ms.pulseSpeed = 0.5f;
        }

        // -- primitive helpers --
        static GameObject Prim(PrimitiveType t, Transform p, string n, Vector3 pos, Vector3 scale)
        {
            GameObject g = GameObject.CreatePrimitive(t);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.name = n; g.transform.SetParent(p, false);
            g.transform.position = pos; g.transform.localScale = scale;
            return g;
        }
        static GameObject Box(Transform p, string n, Vector3 pos, Vector3 s, Material m)
        { var g = Prim(PrimitiveType.Cube, p, n, pos, s); g.GetComponent<Renderer>().sharedMaterial = m; return g; }
        static GameObject Cyl(Transform p, string n, Vector3 pos, Vector3 s, Material m)
        { var g = Prim(PrimitiveType.Cylinder, p, n, pos, s); g.GetComponent<Renderer>().sharedMaterial = m; return g; }
        static GameObject Sphere(Transform p, string n, Vector3 pos, Vector3 s)
        { return Prim(PrimitiveType.Sphere, p, n, pos, s); }
        static GameObject Quad(Transform p, string n, Vector3 pos, Vector3 s)
        { return Prim(PrimitiveType.Quad, p, n, pos, s); }

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
