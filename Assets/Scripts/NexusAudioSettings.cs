using System;
using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// PlayerPrefs-backed music / SFX volume. Wired to settings UI now; audio systems read these when added.
    /// </summary>
    public static class NexusAudioSettings
    {
        const string KeyMusic = "nexus.music_volume";
        const string KeySfx = "nexus.sfx_volume";

        public const float DefaultMusicVolume = 0.7f;
        public const float DefaultSfxVolume = 1f;

        static bool _loaded;
        static float _musicVolume = DefaultMusicVolume;
        static float _sfxVolume = DefaultSfxVolume;

        public static event Action Changed;

        public static float MusicVolume => _musicVolume;
        public static float SfxVolume => _sfxVolume;

        public static void EnsureLoaded()
        {
            if (_loaded)
                return;
            _musicVolume = PlayerPrefs.GetFloat(KeyMusic, DefaultMusicVolume);
            _sfxVolume = PlayerPrefs.GetFloat(KeySfx, DefaultSfxVolume);
            _loaded = true;
            Apply();
        }

        public static void SetMusicVolume(float volume)
        {
            EnsureLoaded();
            float v = Mathf.Clamp01(volume);
            if (Mathf.Approximately(v, _musicVolume))
                return;
            _musicVolume = v;
            PlayerPrefs.SetFloat(KeyMusic, v);
            PlayerPrefs.Save();
            Apply();
        }

        public static void SetSfxVolume(float volume)
        {
            EnsureLoaded();
            float v = Mathf.Clamp01(volume);
            if (Mathf.Approximately(v, _sfxVolume))
                return;
            _sfxVolume = v;
            PlayerPrefs.SetFloat(KeySfx, v);
            PlayerPrefs.Save();
            Apply();
        }

        static void Apply()
        {
            var musicControllers = UnityEngine.Object.FindObjectsOfType<MenuMusicController>();
            for (int i = 0; i < musicControllers.Length; i++)
            {
                if (musicControllers[i] != null)
                    musicControllers[i].ApplyVolume();
            }

            Changed?.Invoke();
        }

        /// <summary>Future SFX one-shots should multiply clip level by this.</summary>
        public static float EffectiveSfxVolume => SfxVolume;
    }
}
