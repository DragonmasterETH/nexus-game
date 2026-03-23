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

        [Range(0f, 1f)] public float Volume = 0.35f;

        AudioSource _src;

        void Awake()
        {
            _src = GetComponent<AudioSource>();
            _src.loop = true;
            _src.playOnAwake = false;
            _src.volume = Volume;
        }

        void Start()
        {
            if (MenuLoop != null && !_src.isPlaying)
            {
                _src.clip = MenuLoop;
                _src.Play();
            }
        }

        public void SetMuted(bool muted)
        {
            _src.mute = muted;
        }
    }
}
