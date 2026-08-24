using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Party.Character
{
    /// <summary>
    /// Front end for PARTY GAME (working title).
    ///
    /// uGUI rather than OnGUI: the home screen is the first thing anyone sees, and OnGUI
    /// reads as a debug overlay however it is styled.
    ///
    /// Steam controls stay VISIBLE but disabled with the reason printed when Steam is
    /// unavailable. Hiding them makes the feature look missing; leaving them live makes
    /// them fail silently, which is the exact failure mode this project keeps hitting.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public enum Panel { Home, Character, Settings, Controls, Multiplayer }

        [Header("Panels")]
        public GameObject homePanel, characterPanel, settingsPanel, controlsPanel, multiplayerPanel;

        [Header("Shared")]
        public CharacterDisplay display;
        public Text statusLabel, steamLabel;

        [Header("Character")]
        public InputField nameField;
        public Text[] lookValues = new Text[7];   // one per customisation row

        [Header("Settings")]
        public Text volumeLabel, qualityLabel, fullscreenLabel, hostVoiceLabel, participantsLabel;

        [Header("Multiplayer")]
        public InputField joinCodeField, joinAddressField;
        public Button steamHostButton, steamJoinButton;

        public string gameScene = "RedLight";

        /// <summary>Customisation rows, in the order they appear.</summary>
        static readonly (string caption, System.Func<string[]> names)[] Rows =
        {
            ("Chassis",   () => CharacterLook.ChassisNames),
            ("Livery",    () => CharacterLook.LiveryNames),
            ("Filament",  () => CharacterLook.FilamentNames),
            ("Glyph",     () => CharacterLook.ShapeNames),
            ("Dome",      () => CharacterLook.DomeNames),
            ("Mask",      () => CharacterLook.MaskNames),
            ("Accessory", () => CharacterLook.AccessoryNames),
        };

        public static int RowCount => Rows.Length;
        public static string RowCaption(int i) => Rows[i].caption;

        void Start()
        {
            PlayerProfile.Apply();
            if (nameField != null)
            {
                nameField.text = PlayerProfile.Name;
                nameField.onEndEdit.AddListener(v => { PlayerProfile.Name = v; Refresh(); });
            }
            Show((int)Panel.Home);
        }

        public void Show(int panel)
        {
            if (homePanel != null)        homePanel.SetActive(panel == (int)Panel.Home);
            if (characterPanel != null)   characterPanel.SetActive(panel == (int)Panel.Character);
            if (settingsPanel != null)    settingsPanel.SetActive(panel == (int)Panel.Settings);
            if (controlsPanel != null)    controlsPanel.SetActive(panel == (int)Panel.Controls);
            if (multiplayerPanel != null) multiplayerPanel.SetActive(panel == (int)Panel.Multiplayer);
            Refresh();
        }

        // ---- character ----

        /// <summary>Encoded as row*10 + direction so one int fits a UnityEvent listener.</summary>
        public void StepLook(int packed)
        {
            int row = packed / 10;
            int dir = (packed % 10) == 1 ? 1 : -1;
            LookConfig c = PlayerProfile.Look;
            int n = Rows[row].names().Length;

            switch (row)
            {
                case 0: c.chassis   = CharacterLook.Wrap(c.chassis + dir, n); break;
                case 1: c.livery    = CharacterLook.Wrap(c.livery + dir, n); break;
                case 2: c.filament  = CharacterLook.Wrap(c.filament + dir, n); break;
                case 3: c.shape     = CharacterLook.Wrap(c.shape + dir, n); break;
                case 4: c.dome      = CharacterLook.Wrap(c.dome + dir, n); break;
                case 5: c.mask      = CharacterLook.Wrap(c.mask + dir, n); break;
                case 6: c.accessory = CharacterLook.Wrap(c.accessory + dir, n); break;
            }
            PlayerProfile.Look = c;
            display?.Rebuild();
            Refresh();
        }

        public void Randomise()
        {
            PlayerProfile.Look = new LookConfig
            {
                chassis   = Random.Range(0, CharacterLook.ChassisNames.Length),
                livery    = Random.Range(0, CharacterLook.LiveryNames.Length),
                filament  = Random.Range(0, CharacterLook.FilamentNames.Length),
                shape     = Random.Range(0, CharacterLook.ShapeNames.Length),
                dome      = Random.Range(0, CharacterLook.DomeNames.Length),
                mask      = Random.Range(0, CharacterLook.MaskNames.Length),
                accessory = Random.Range(0, CharacterLook.AccessoryNames.Length),
            };
            display?.Rebuild();
            Refresh();
        }

        // ---- settings ----

        public void StepVolume(int d)      { PlayerProfile.Volume = Mathf.Clamp01(PlayerProfile.Volume + d * 0.1f); Refresh(); }
        public void StepQuality(int d)     { PlayerProfile.Quality = Mathf.Clamp(PlayerProfile.Quality + d, 0, QualitySettings.names.Length - 1); Refresh(); }
        public void ToggleFullscreen(int _) { PlayerProfile.Fullscreen = !PlayerProfile.Fullscreen; Refresh(); }
        public void ToggleHostVoice(int _) { PlayerProfile.HostVoiceEnabled = !PlayerProfile.HostVoiceEnabled; Refresh(); }
        public void StepParticipants(int d) { PlayerProfile.Participants = PlayerProfile.Participants + d; Refresh(); }

        // ---- play ----

        public void PlayLocal()   => Launch(PendingSetup.Mode.HostLocal);
        public void HostOnSteam() => Launch(PendingSetup.Mode.HostSteam);

        public void JoinByCode()
        {
            PendingSetup.code = (joinCodeField != null ? joinCodeField.text : "").Trim().ToUpperInvariant();
            Launch(PendingSetup.Mode.JoinCode);
        }

        public void JoinByAddress()
        {
            PendingSetup.address = string.IsNullOrWhiteSpace(joinAddressField?.text) ? "localhost" : joinAddressField.text.Trim();
            Launch(PendingSetup.Mode.JoinAddress);
        }

        void Launch(PendingSetup.Mode mode)
        {
            PendingSetup.mode = mode;
            PendingSetup.participants = PlayerProfile.Participants;
            SceneManager.LoadScene(gameScene);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- display ----

        public void Refresh()
        {
            LookConfig c = PlayerProfile.Look;
            int[] idx = { c.chassis, c.livery, c.filament, c.shape, c.dome, c.mask, c.accessory };
            for (int i = 0; i < lookValues.Length && i < Rows.Length; i++)
                if (lookValues[i] != null)
                {
                    string[] names = Rows[i].names();
                    lookValues[i].text = names[CharacterLook.Wrap(idx[i], names.Length)];
                }

            if (volumeLabel != null)      volumeLabel.text = Mathf.RoundToInt(PlayerProfile.Volume * 100f) + "%";
            if (qualityLabel != null)     qualityLabel.text = QualitySettings.names[PlayerProfile.Quality];
            if (fullscreenLabel != null)  fullscreenLabel.text = PlayerProfile.Fullscreen ? "On" : "Off";
            if (hostVoiceLabel != null)   hostVoiceLabel.text = PlayerProfile.HostVoiceEnabled ? "On" : "Off";
            if (participantsLabel != null)
                participantsLabel.text = PlayerProfile.Participants + "  (you + " + (PlayerProfile.Participants - 1) + " bots)";

            bool steam = SteamBoot.Ready;
            if (steamLabel != null)
                steamLabel.text = steam ? "Steam: connected" : "Steam unavailable — " + SteamBoot.FailureReason;
            if (steamHostButton != null) steamHostButton.interactable = steam;
            if (steamJoinButton != null) steamJoinButton.interactable = steam;
        }
    }

    /// <summary>What the menu chose, read once by the game scene. Static so it survives the load.</summary>
    public static class PendingSetup
    {
        public enum Mode { None, HostLocal, HostSteam, JoinCode, JoinAddress }
        public static Mode   mode = Mode.None;
        public static int    participants = 4;
        public static string code = "";
        public static string address = "localhost";

        public static Mode Consume() { Mode m = mode; mode = Mode.None; return m; }
    }
}
