using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Plays looping menu music when a clip is assigned. Attach to any persistent object or the Bootstrap object.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MenuMusicController : MonoBehaviour
    {
        [Tooltip("Optional: assign an AudioClip in the Inspector.")]
        public AudioClip MenuLoop;

        AudioSource _src;

        void Awake()
        {
            NexusAudioSettings.EnsureLoaded();
            _src = GetComponent<AudioSource>();
            _src.loop = true;
            _src.playOnAwake = false;
            ApplyVolume();
        }

        void OnEnable() => NexusAudioSettings.Changed += ApplyVolume;
        void OnDisable() => NexusAudioSettings.Changed -= ApplyVolume;

        void Start()
        {
            if (MenuLoop != null && !_src.isPlaying)
            {
                _src.clip = MenuLoop;
                _src.Play();
            }
        }

        public void ApplyVolume()
        {
            if (_src == null)
                return;
            NexusAudioSettings.EnsureLoaded();
            _src.volume = NexusAudioSettings.MusicVolume;
        }

        public void SetMuted(bool muted)
        {
            _src.mute = muted;
        }
    }
}
