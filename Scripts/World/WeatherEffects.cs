using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Efectos visuales del clima: lluvia, nieve, viento (que inclina las partículas)
/// y rayos durante las tormentas. Todo se mezcla según el peso de cada clima en
/// el WorldState, así que aparece y desaparece poco a poco.
///
/// Espera dos sistemas de partículas hijos llamados "Rain" y "Snow"
/// (la herramienta Tools > World > Preparar iluminación y clima los crea).
/// </summary>
public class WeatherEffects : MonoBehaviour
{
    [Header("Referencias (se buscan solas si se dejan vacías)")]
    [SerializeField] private WorldState worldState;
    [SerializeField] private ParticleSystem rain;
    [SerializeField] private ParticleSystem snow;

    [Tooltip("Lo que siguen las partículas. Si se deja vacío, la cámara principal.")]
    [SerializeField] private Transform followTarget;

    [Header("Cantidad de partículas por segundo")]
    [SerializeField, Min(0f)] private float rainRate = 1200f;
    [SerializeField, Min(0f)] private float stormRainRate = 2800f;
    [SerializeField, Min(0f)] private float snowRate = 450f;

    [Header("Viento")]
    [Tooltip("Dirección horizontal del viento (se normaliza).")]
    [SerializeField] private Vector2 windDirection = new Vector2(1f, 0.3f);

    [Tooltip("Cuánto empuja el viento a la lluvia con fuerza máxima.")]
    [SerializeField, Min(0f)] private float rainWindForce = 12f;

    [Tooltip("Cuánto empuja el viento a la nieve con fuerza máxima.")]
    [SerializeField, Min(0f)] private float snowWindForce = 3f;

    [Header("Rayos (durante las tormentas)")]
    [Tooltip("Segundos entre rayos: mínimo y máximo.")]
    [SerializeField] private Vector2 lightningInterval = new Vector2(7f, 20f);

    /// <summary>Intensidad del destello del último rayo (0 a 1). La lee AtmosphereController.</summary>
    public float LightningFlash { get; private set; }

    /// <summary>Fuerza del viento en este momento (0 a 1), por si otros sistemas la necesitan.</summary>
    public float WindStrength { get; private set; }

    /// <summary>Se dispara cada vez que cae un rayo (para conectar un sonido de trueno, por ejemplo).</summary>
    public event Action LightningStruck;

    private float lightningTimer;
    private float secondFlashTimer = -1f;

    private void Awake()
    {
        if (worldState == null) worldState = FindFirstObjectByType<WorldState>();

        if (rain == null) rain = FindSystem("Rain");
        if (snow == null) snow = FindSystem("Snow");

        lightningTimer = Random.Range(lightningInterval.x, lightningInterval.y);
    }

    private ParticleSystem FindSystem(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<ParticleSystem>() : null;
    }

    private void Update()
    {
        if (worldState == null)
        {
            return;
        }

        float dt = Time.deltaTime;

        float wRain = worldState.GetWeatherWeight(WeatherType.Rain);
        float wStorm = worldState.GetWeatherWeight(WeatherType.Storm);
        float wSnow = worldState.GetWeatherWeight(WeatherType.Snow);

        // --- Viento: se mezcla según el clima ---
        WindStrength =
            worldState.GetWeatherWeight(WeatherType.Wind) * 1.0f +
            wStorm * 0.9f +
            wRain * 0.35f +
            wSnow * 0.2f +
            worldState.GetWeatherWeight(WeatherType.Cloudy) * 0.2f +
            worldState.GetWeatherWeight(WeatherType.Sunny) * 0.1f;

        WindStrength = Mathf.Clamp01(WindStrength);

        Vector2 wind = windDirection.sqrMagnitude > 0.0001f ? windDirection.normalized : Vector2.right;

        // --- Partículas ---
        ApplyParticles(rain, wRain * rainRate + wStorm * stormRainRate, wind, rainWindForce);
        ApplyParticles(snow, wSnow * snowRate, wind, snowWindForce);

        UpdateLightning(dt, wStorm);
    }

    private void LateUpdate()
    {
        // Las partículas se mueven con la cámara (o el objetivo), pero caen en el mundo
        Transform target = followTarget;

        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }

        if (target != null)
        {
            transform.position = target.position;
        }
    }

    private void ApplyParticles(ParticleSystem system, float rate, Vector2 wind, float maxForce)
    {
        if (system == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = system.emission;
        bool active = rate > 0.5f;

        emission.enabled = active;
        emission.rateOverTime = rate;

        if (active && !system.isPlaying)
        {
            system.Play();
        }

        // El viento empuja las partículas de lado
        ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
        force.enabled = true;
        force.space = ParticleSystemSimulationSpace.World;
        force.x = wind.x * WindStrength * maxForce;
        force.y = 0f;
        force.z = wind.y * WindStrength * maxForce;
    }

    // Durante las tormentas caen rayos de vez en cuando, a veces con un segundo destello
    private void UpdateLightning(float dt, float stormWeight)
    {
        if (stormWeight > 0.5f)
        {
            lightningTimer -= dt;

            if (lightningTimer <= 0f)
            {
                LightningFlash = 1f;
                secondFlashTimer = Random.value < 0.6f ? Random.Range(0.12f, 0.25f) : -1f;
                lightningTimer = Random.Range(lightningInterval.x, lightningInterval.y);

                LightningStruck?.Invoke();
            }
        }

        if (secondFlashTimer > 0f)
        {
            secondFlashTimer -= dt;

            if (secondFlashTimer <= 0f)
            {
                LightningFlash = 0.8f;
            }
        }

        LightningFlash = Mathf.MoveTowards(LightningFlash, 0f, dt * 3.5f);
    }
}
