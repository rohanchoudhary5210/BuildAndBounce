using BuildAndBounce.Level;
using UnityEngine;

namespace BuildAndBounce.Player
{
    /// <summary>
    /// Manages audio feedback for player actions (jump, land, dash) and milestone events.
    /// Synthesizes procedural sound clips at startup if no custom audio clips are assigned,
    /// ensuring production audio feedback without external asset dependencies.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAudio : MonoBehaviour
    {
        [Header("Audio Source")]
        [SerializeField] private AudioSource _audioSource;

        [Header("Custom Sound Overrides (Optional)")]
        [Tooltip("Custom jump sound effect. If unassigned, procedural synth sound is used.")]
        [SerializeField] private AudioClip _jumpSound;

        [Tooltip("Custom landing sound effect. If unassigned, procedural synth sound is used.")]
        [SerializeField] private AudioClip _landSound;

        [Tooltip("Custom dash sound effect. If unassigned, procedural synth sound is used.")]
        [SerializeField] private AudioClip _dashSound;

        [Tooltip("Custom checkpoint chime. If unassigned, procedural synth sound is used.")]
        [SerializeField] private AudioClip _checkpointSound;

        [Tooltip("Custom finish fanfare. If unassigned, procedural synth sound is used.")]
        [SerializeField] private AudioClip _finishSound;

        [Header("Tuning")]
        [Range(0f, 1f)]
        [SerializeField] private float _masterSfxVolume = 0.85f;

        [SerializeField] private float _pitchVariation = 0.08f;

        private PlayerController _controller;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();

            if (_audioSource == null)
            {
                if (!TryGetComponent(out _audioSource))
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D player audio for clarity

            GenerateProceduralAudioIfMissing();
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.OnJumped += HandleJumped;
                _controller.OnLanded += HandleLanded;
                _controller.OnDashed += HandleDashed;
            }

            Checkpoint.OnCheckpointActivated += HandleCheckpoint;
            CourseFinish.OnCourseFinished += HandleFinish;
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnJumped -= HandleJumped;
                _controller.OnLanded -= HandleLanded;
                _controller.OnDashed -= HandleDashed;
            }

            Checkpoint.OnCheckpointActivated -= HandleCheckpoint;
            CourseFinish.OnCourseFinished -= HandleFinish;
        }

        private void HandleJumped()
        {
            PlayClipWithPitchVariation(_jumpSound, volume: 0.7f);
        }

        private void HandleLanded()
        {
            float speed = _controller != null ? _controller.LastImpactSpeed : 10f;
            float volume = Mathf.Clamp01(speed / 20f) * 0.9f;
            PlayClipWithPitchVariation(_landSound, volume: volume);
        }

        private void HandleDashed()
        {
            PlayClipWithPitchVariation(_dashSound, volume: 0.8f);
        }

        private void HandleCheckpoint(Checkpoint cp)
        {
            PlayClip(_checkpointSound, volume: 1.0f, pitch: 1.0f);
        }

        private void HandleFinish()
        {
            PlayClip(_finishSound, volume: 1.0f, pitch: 1.0f);
        }

        private void PlayClipWithPitchVariation(AudioClip clip, float volume)
        {
            if (clip == null) return;
            float pitch = 1f + Random.Range(-_pitchVariation, _pitchVariation);
            PlayClip(clip, volume, pitch);
        }

        private void PlayClip(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || _audioSource == null) return;
            _audioSource.pitch = pitch;
            _audioSource.PlayOneShot(clip, volume * _masterSfxVolume);
        }

        private void GenerateProceduralAudioIfMissing()
        {
            const int sampleRate = 44100;

            if (_jumpSound == null)
            {
                _jumpSound = CreateSynthClip("Jump_Proc", sampleRate, 0.12f, (t, dur) =>
                {
                    // Upward chirp: 280Hz -> 620Hz
                    float p = t / dur;
                    float freq = Mathf.Lerp(280f, 620f, p);
                    float env = Mathf.Sin(p * Mathf.PI);
                    return Mathf.Sin(2f * Mathf.PI * freq * t) * env;
                });
            }

            if (_landSound == null)
            {
                _landSound = CreateSynthClip("Land_Proc", sampleRate, 0.14f, (t, dur) =>
                {
                    // Low thud: 180Hz -> 55Hz with rapid decay
                    float p = t / dur;
                    float freq = Mathf.Lerp(180f, 55f, p);
                    float env = Mathf.Exp(-p * 12f);
                    return Mathf.Sin(2f * Mathf.PI * freq * t) * env;
                });
            }

            if (_dashSound == null)
            {
                _dashSound = CreateSynthClip("Dash_Proc", sampleRate, 0.18f, (t, dur) =>
                {
                    // Whoosh / filtered noise burst
                    float p = t / dur;
                    float env = Mathf.Sin(p * Mathf.PI);
                    float noise = Random.Range(-1f, 1f);
                    return noise * env * 0.7f;
                });
            }

            if (_checkpointSound == null)
            {
                _checkpointSound = CreateSynthClip("Checkpoint_Proc", sampleRate, 0.25f, (t, dur) =>
                {
                    // Two-tone bell chime: C5 (523Hz) then E5 (659Hz)
                    float freq = t < dur * 0.45f ? 523f : 659f;
                    float env = Mathf.Exp(-(t % (dur * 0.5f)) * 14f);
                    return (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.4f * Mathf.Sin(4f * Mathf.PI * freq * t)) * env;
                });
            }

            if (_finishSound == null)
            {
                _finishSound = CreateSynthClip("Finish_Proc", sampleRate, 0.5f, (t, dur) =>
                {
                    // Three-tone victory arpeggio: C5 (523Hz) -> E5 (659Hz) -> G5 (784Hz)
                    float step = dur / 3f;
                    float freq = t < step ? 523f : (t < step * 2f ? 659f : 784f);
                    float env = Mathf.Exp(-(t % step) * 8f);
                    return Mathf.Sin(2f * Mathf.PI * freq * t) * env;
                });
            }
        }

        private static AudioClip CreateSynthClip(string name, int sampleRate, float duration, System.Func<float, float, float> sampleFunc)
        {
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = sampleFunc(t, duration);
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}