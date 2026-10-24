using System;
using Unity.Cinemachine;
using UnityEngine;

namespace BuildAndBounce.Player
{
    /// <summary>
    /// Handles procedural visual game feel for the player:
    /// 1. Dynamic squash-and-stretch on jump, land, and dash using a damped spring.
    /// 2. Procedural landing dust / jump puff particle effects.
    /// 3. Cinemachine camera shake impulse on high-impact landings.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerVisuals : MonoBehaviour
    {
        [Header("Target Mesh/Model")]
        [Tooltip("Child transform that holds the visible mesh. Scaled independently of physics collider.")]
        [SerializeField] private Transform _visualModel;

        [Header("Squash & Stretch Springs")]
        [Tooltip("Scale deformation when executing a jump (stretched vertically).")]
        [SerializeField] private Vector3 _jumpScale = new Vector3(0.75f, 1.35f, 0.75f);

        [Tooltip("Scale deformation when landing softly.")]
        [SerializeField] private Vector3 _softLandScale = new Vector3(1.2f, 0.8f, 1.2f);

        [Tooltip("Scale deformation on maximum fall velocity impact.")]
        [SerializeField] private Vector3 _hardLandScale = new Vector3(1.5f, 0.5f, 1.5f);

        [Tooltip("Scale deformation while dashing.")]
        [SerializeField] private Vector3 _dashScale = new Vector3(0.7f, 0.7f, 1.4f);

        [Tooltip("Natural oscillation frequency of the spring (higher = snappier bounce).")]
        [SerializeField] private float _springStiffness = 180f;

        [Tooltip("Damping ratio to eliminate jitter (higher = settles faster).")]
        [SerializeField] private float _springDamping = 16f;

        [Header("Camera Shake (Cinemachine Impulse)")]
        [Tooltip("Optional impulse source for triggering camera shake on hard falls.")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        [Tooltip("Minimum downward speed before landing triggers camera shake.")]
        [SerializeField] private float _cameraShakeSpeedThreshold = 10f;

        [Tooltip("Multiplier translating landing velocity to impulse strength.")]
        [SerializeField] private float _impulseMultiplier = 0.04f;

        [Header("Procedural Particles")]
        [Tooltip("Enables built-in procedural dust puffs when no custom particle prefab is assigned.")]
        [SerializeField] private bool _enableDustPuffs = true;

        [Tooltip("Base color for dust puffs.")]
        [SerializeField] private Color _dustColor = new Color(0.9f, 0.9f, 0.95f, 0.7f);

        private PlayerController _controller;
        private ParticleSystem _dustParticles;

        private Vector3 _currentScale = Vector3.one;
        private Vector3 _scaleVelocity = Vector3.zero;
        private readonly Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();

            if (_impulseSource == null)
            {
                TryGetComponent(out _impulseSource);
            }

            if (_visualModel == null)
            {
                var visualChild = transform.Find("Visual");
                _visualModel = visualChild != null ? visualChild : transform;
            }

            if (_enableDustPuffs)
            {
                SetupDustParticleSystem();
            }
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.OnJumped += HandleJumped;
                _controller.OnLanded += HandleLanded;
                _controller.OnDashed += HandleDashed;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnJumped -= HandleJumped;
                _controller.OnLanded -= HandleLanded;
                _controller.OnDashed -= HandleDashed;
            }
        }

        private void Update()
        {
            UpdateSpring(Time.deltaTime);
        }

        private void UpdateSpring(float dt)
        {
            if (_visualModel == null || dt <= 0f) return;

            // Hooke's Law spring with damping towards Vector3.one
            Vector3 displacement = _currentScale - _baseScale;
            Vector3 springForce = -_springStiffness * displacement - _springDamping * _scaleVelocity;

            _scaleVelocity += springForce * dt;
            _currentScale += _scaleVelocity * dt;

            _visualModel.localScale = _currentScale;
        }

        private void ApplyDeformation(Vector3 targetDeformation)
        {
            _currentScale = targetDeformation;
        }

        private void HandleJumped()
        {
            ApplyDeformation(_jumpScale);
            EmitDustBurst(count: 8, speed: 2f);
        }

        private void HandleLanded()
        {
            float impactSpeed = _controller.LastImpactSpeed;
            float t = Mathf.Clamp01(impactSpeed / 25f);
            Vector3 landSquash = Vector3.Lerp(_softLandScale, _hardLandScale, t);
            ApplyDeformation(landSquash);

            // Emit landing dust ring proportional to impact
            int count = Mathf.RoundToInt(Mathf.Lerp(6, 16, t));
            EmitDustBurst(count: count, speed: Mathf.Lerp(1.5f, 4f, t));

            // Camera shake on hard impacts
            if (impactSpeed >= _cameraShakeSpeedThreshold && _impulseSource != null)
            {
                float strength = (impactSpeed - _cameraShakeSpeedThreshold) * _impulseMultiplier;
                _impulseSource.GenerateImpulse(Vector3.down * Mathf.Clamp01(strength));
            }
        }

        private void HandleDashed()
        {
            ApplyDeformation(_dashScale);
            EmitDustBurst(count: 10, speed: 3f);
        }

        private void SetupDustParticleSystem()
        {
            var dustObj = new GameObject("PlayerDustPuffs");
            dustObj.transform.SetParent(transform, false);
            dustObj.transform.localPosition = new Vector3(0f, -0.9f, 0f);

            _dustParticles = dustObj.AddComponent<ParticleSystem>();
            var pRenderer = dustObj.GetComponent<ParticleSystemRenderer>();

            // URP Particle shader or fallback
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                pRenderer.material = new Material(shader) { color = _dustColor };
            }

            var main = _dustParticles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.4f;
            main.startLifetime = 0.35f;
            main.startSpeed = 2.5f;
            main.startSize = 0.25f;
            main.startColor = _dustColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.None;

            var shape = _dustParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var sizeOverLifetime = _dustParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.4f);
            curve.AddKey(0.2f, 1f);
            curve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var colorOverLifetime = _dustParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            var emission = _dustParticles.emission;
            emission.rateOverTime = 0f;
        }

        private void EmitDustBurst(int count, float speed)
        {
            if (_dustParticles == null) return;

            var main = _dustParticles.main;
            main.startSpeed = speed;
            _dustParticles.Emit(count);
        }
    }
}