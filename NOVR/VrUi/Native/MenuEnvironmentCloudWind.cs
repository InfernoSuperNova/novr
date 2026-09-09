using UnityEngine;

namespace NOVR.VrUi.Native;

public sealed class MenuEnvironmentCloudWind : MonoBehaviour
{
    private static readonly Vector3 WindVelocity = new(75f, 0.2f, 2f);

    private ParticleSystem[] _particleSystems = new ParticleSystem[0];

    private void Awake()
    {
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    private void OnEnable()
    {
        ConfigureParticleSystems(true);
    }

    private void OnTransformChildrenChanged()
    {
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        ConfigureParticleSystems(false);
    }

    private void ConfigureParticleSystems(bool restart)
    {
        for (var index = 0; index < _particleSystems.Length; index++)
        {
            var particleSystem = _particleSystems[index];
            if (particleSystem == null) continue;

            var velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(WindVelocity.x);
            velocity.y = new ParticleSystem.MinMaxCurve(WindVelocity.y);
            velocity.z = new ParticleSystem.MinMaxCurve(WindVelocity.z);
            velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f);

            if (restart && particleSystem.gameObject.activeInHierarchy)
            {
                if (ShouldRestartParticleSystem(particleSystem))
                {
                    particleSystem.Clear(true);
                }

                particleSystem.Play(true);
            }
        }
    }

    private static bool ShouldRestartParticleSystem(ParticleSystem particleSystem)
    {
        var emission = particleSystem.emission;
        if (!emission.enabled) return false;

        var name = particleSystem.gameObject.name;
        return !name.Contains("flyThrough") &&
               !string.Equals(name, "lightning", System.StringComparison.OrdinalIgnoreCase);
    }
}
