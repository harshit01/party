using UnityEngine;

namespace Party.Character
{
    /// <summary>
    /// Name, look and settings, persisted between sessions.
    ///
    /// PlayerPrefs on purpose: these are a handful of ints, and committing to a save
    /// format is a decision worth deferring until there is something worth saving.
    /// </summary>
    public static class PlayerProfile
    {
        public static string Name
        {
            get { string n = PlayerPrefs.GetString("party.name", ""); return string.IsNullOrWhiteSpace(n) ? "Player" : n; }
            set { PlayerPrefs.SetString("party.name", (value ?? "").Trim()); PlayerPrefs.Save(); }
        }

        public static LookConfig Look
        {
            get => new LookConfig
            {
                chassis   = PlayerPrefs.GetInt("party.chassis", 0),
                livery    = PlayerPrefs.GetInt("party.livery", 0),
                filament  = PlayerPrefs.GetInt("party.filament", 0),
                shape     = PlayerPrefs.GetInt("party.shape", 0),
                dome      = PlayerPrefs.GetInt("party.dome", 0),
                mask      = PlayerPrefs.GetInt("party.mask", 0),
                accessory = PlayerPrefs.GetInt("party.accessory", 0),
            };
            set
            {
                PlayerPrefs.SetInt("party.chassis",   value.chassis);
                PlayerPrefs.SetInt("party.livery",    value.livery);
                PlayerPrefs.SetInt("party.filament",  value.filament);
                PlayerPrefs.SetInt("party.shape",     value.shape);
                PlayerPrefs.SetInt("party.dome",      value.dome);
                PlayerPrefs.SetInt("party.mask",      value.mask);
                PlayerPrefs.SetInt("party.accessory", value.accessory);
                PlayerPrefs.Save();
            }
        }

        // ---- settings ----
        public static float Volume
        {
            get => PlayerPrefs.GetFloat("party.volume", 0.8f);
            set { PlayerPrefs.SetFloat("party.volume", Mathf.Clamp01(value)); AudioListener.volume = Mathf.Clamp01(value); PlayerPrefs.Save(); }
        }

        public static int Quality
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("party.quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            set { PlayerPrefs.SetInt("party.quality", value); QualitySettings.SetQualityLevel(value, true); PlayerPrefs.Save(); }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt("party.fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            set { PlayerPrefs.SetInt("party.fullscreen", value ? 1 : 0); Screen.fullScreen = value; PlayerPrefs.Save(); }
        }

        /// <summary>Lets you play with Barnaby switched off - the AI must be removable.</summary>
        public static bool HostVoiceEnabled
        {
            get => PlayerPrefs.GetInt("party.hostvoice", 1) == 1;
            set { PlayerPrefs.SetInt("party.hostvoice", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int Participants
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("party.participants", 4), 2, 8);
            set { PlayerPrefs.SetInt("party.participants", Mathf.Clamp(value, 2, 8)); PlayerPrefs.Save(); }
        }

        public static void Apply()
        {
            AudioListener.volume = Volume;
            QualitySettings.SetQualityLevel(Quality, true);
        }
    }
}
