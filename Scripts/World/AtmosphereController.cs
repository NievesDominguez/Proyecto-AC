using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Aspecto de la escena para un clima concreto. El controlador mezcla estos valores
/// según el peso de cada clima, así las transiciones son suaves.
/// </summary>
[Serializable]
public struct WeatherLook
{
    public WeatherType weather;

    [Tooltip("Multiplicador de la luz del sol.")]
    [Min(0f)] public float sunIntensity;

    [Tooltip("Multiplicador de la luz ambiente.")]
    [Min(0f)] public float ambient;

    [Tooltip("Multiplicador del brillo del cielo.")]
    [Min(0f)] public float skyExposure;

    [Tooltip("Densidad de la niebla (0,002 = casi nada; 0,03 = muy espesa).")]
    [Min(0f)] public float fogDensity;

    [Tooltip("Color por el que se multiplica el color de la niebla.")]
    public Color fogTint;

    [Tooltip("Cuánto se apagan los colores (0 = normales, 1 = grises).")]
    [Range(0f, 1f)] public float desaturation;

    [Tooltip("Fuerza de las sombras (con cielo cubierto son más suaves).")]
    [Range(0f, 1f)] public float shadowStrength;

    public WeatherLook(
        WeatherType weather,
        float sunIntensity,
        float ambient,
        float skyExposure,
        float fogDensity,
        Color fogTint,
        float desaturation,
        float shadowStrength)
    {
        this.weather = weather;
        this.sunIntensity = sunIntensity;
        this.ambient = ambient;
        this.skyExposure = skyExposure;
        this.fogDensity = fogDensity;
        this.fogTint = fogTint;
        this.desaturation = desaturation;
        this.shadowStrength = shadowStrength;
    }
}

/// <summary>Una de las lunas del planeta (el diseño prevé dos).</summary>
[Serializable]
public class MoonSettings
{
    public string name = "Luna";

    [Tooltip("Días que tarda en completar un ciclo de fases.")]
    [Min(0.5f)] public float periodDays = 8f;

    [Tooltip("Desplazamiento del ciclo (0 a 1) para que no coincidan las fases.")]
    [Range(0f, 1f)] public float phaseOffset = 0f;

    public Color color = new Color(0.75f, 0.85f, 1f);

    [Tooltip("Intensidad de la luz con luna llena en lo alto del cielo.")]
    [Min(0f)] public float maxIntensity = 0.18f;
}

/// <summary>
/// Controla la iluminación y la ambientación de la escena según el WorldState:
///  - posición del sol (hora, época del año y latitud del planeta);
///  - luz de las lunas por la noche;
///  - color del sol, de la luz ambiente, de la niebla y del cielo;
///  - efecto del clima (nublado, lluvia, niebla...) y de la estación.
///
/// Se encarga de todo por código; solo necesita una luz direccional.
/// </summary>
public class AtmosphereController : MonoBehaviour
{
    [Header("Referencias (se buscan solas si se dejan vacías)")]
    [SerializeField] private WorldState worldState;
    [SerializeField] private Light sunLight;
    [SerializeField] private WeatherEffects weatherEffects;

    [Header("Posición del sol")]
    [Tooltip("Latitud del lugar. Cerca del ecuador los días duran siempre igual; cerca de los polos varían mucho.")]
    [SerializeField, Range(-80f, 80f)] private float latitude = 40f;

    [Tooltip("Inclinación del eje del planeta. Más inclinación = estaciones más marcadas.")]
    [SerializeField, Range(0f, 45f)] private float axialTilt = 23.5f;

    [Tooltip("Giro del 'norte' respecto al eje Z del mundo. Cambia por dónde sale y se pone el sol.")]
    [SerializeField, Range(0f, 360f)] private float northDirection = 0f;

    [Tooltip("Suavizado del giro de la luz (más alto = sigue antes al astro).")]
    [SerializeField, Range(0.5f, 20f)] private float rotationSmoothing = 3f;

    [Header("Sol")]
    [SerializeField, Min(0f)] private float sunMaxIntensity = 1.2f;
    [SerializeField] private AnimationCurve sunIntensityByElevation = MakeCurve(
        (-6f, 0f), (0f, 0.12f), (8f, 0.55f), (25f, 0.9f), (60f, 1f));

    [Tooltip("Color del sol según su altura (0 = horizonte, 1 = 40 grados o más).")]
    [SerializeField] private Gradient sunColorByElevation = MakeGradient(
        (0f, new Color(1f, 0.45f, 0.2f)),
        (0.12f, new Color(1f, 0.62f, 0.35f)),
        (0.35f, new Color(1f, 0.88f, 0.72f)),
        (1f, new Color(1f, 0.96f, 0.9f)));

    [Header("Lunas (se usan de noche)")]
    [SerializeField] private MoonSettings[] moons =
    {
        new MoonSettings
        {
            name = "Luna A", periodDays = 8f, phaseOffset = 0.15f,
            color = new Color(0.72f, 0.84f, 1f), maxIntensity = 0.18f
        },
        new MoonSettings
        {
            name = "Luna B", periodDays = 21f, phaseOffset = 0.6f,
            color = new Color(0.9f, 0.75f, 1f), maxIntensity = 0.12f
        }
    };

    [Header("Luz ambiente (según la luz del día: 0 = noche, 1 = día)")]
    [SerializeField, Min(0f)] private float ambientIntensity = 1f;

    [SerializeField] private Gradient ambientSky = MakeGradient(
        (0f, new Color(0.03f, 0.04f, 0.09f)),
        (0.35f, new Color(0.25f, 0.22f, 0.32f)),
        (0.7f, new Color(0.38f, 0.48f, 0.68f)),
        (1f, new Color(0.45f, 0.6f, 0.85f)));

    [SerializeField] private Gradient ambientEquator = MakeGradient(
        (0f, new Color(0.03f, 0.035f, 0.07f)),
        (0.35f, new Color(0.3f, 0.24f, 0.26f)),
        (1f, new Color(0.55f, 0.58f, 0.62f)));

    [SerializeField] private Gradient ambientGround = MakeGradient(
        (0f, new Color(0.015f, 0.015f, 0.025f)),
        (0.35f, new Color(0.15f, 0.12f, 0.1f)),
        (1f, new Color(0.3f, 0.27f, 0.22f)));

    [Header("Niebla")]
    [SerializeField] private Gradient fogColor = MakeGradient(
        (0f, new Color(0.04f, 0.05f, 0.1f)),
        (0.3f, new Color(0.55f, 0.4f, 0.35f)),
        (1f, new Color(0.7f, 0.78f, 0.88f)));

    [Header("Cielo")]
    [Tooltip("Brillo del cielo de noche respecto al de día (el cielo procedural se ilumina con la luna).")]
    [SerializeField, Range(0f, 1f)] private float nightSkyExposure = 0.4f;

    [Header("Estaciones (color que se mezcla con la luz y la niebla)")]
    [SerializeField] private Color[] seasonTints =
    {
        new Color(0.97f, 1.00f, 0.95f),   // Primavera
        new Color(1.00f, 0.98f, 0.92f),   // Verano
        new Color(1.00f, 0.92f, 0.85f),   // Otoño
        new Color(0.92f, 0.96f, 1.00f)    // Invierno
    };

    [Header("Aspecto de cada clima")]
    [SerializeField] private WeatherLook[] looks = DefaultLooks();

    /// <summary>Altura del sol sobre el horizonte, en grados (negativa = bajo el horizonte).</summary>
    public float SunElevation { get; private set; }

    /// <summary>0 = noche cerrada, 1 = pleno día.</summary>
    public float Daylight { get; private set; }

    private Material skyMaterial;
    private float baseSkyExposure = 1.3f;
    private Color baseSkyTint = new Color(0.5f, 0.5f, 0.5f);
    private string skyTintProperty;
    private bool firstFrame = true;

    // ------------------------------------------------------------------
    // CICLO DE VIDA
    // ------------------------------------------------------------------
    private void Awake()
    {
        if (worldState == null) worldState = FindFirstObjectByType<WorldState>();
        if (weatherEffects == null) weatherEffects = FindFirstObjectByType<WeatherEffects>();

        if (sunLight == null)
        {
            sunLight = RenderSettings.sun;
        }

        if (sunLight == null)
        {
            foreach (Light candidate in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (candidate.type == LightType.Directional)
                {
                    sunLight = candidate;
                    break;
                }
            }
        }

        if (worldState == null || sunLight == null)
        {
            Debug.LogError(
                "AtmosphereController: hace falta un WorldState y una luz direccional en la escena.",
                this
            );
            enabled = false;
            return;
        }

        if (looks == null || looks.Length == 0)
        {
            looks = DefaultLooks();
        }

        RenderSettings.sun = sunLight;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        PrepareSkybox();
    }

    // Trabajamos con una copia del material del cielo para no modificar el archivo del proyecto
    private void PrepareSkybox()
    {
        if (RenderSettings.skybox == null)
        {
            return;
        }

        skyMaterial = new Material(RenderSettings.skybox);
        RenderSettings.skybox = skyMaterial;

        if (skyMaterial.HasProperty("_Exposure"))
        {
            baseSkyExposure = skyMaterial.GetFloat("_Exposure");
        }

        if (skyMaterial.HasProperty("_SkyTint"))
        {
            skyTintProperty = "_SkyTint";
        }
        else if (skyMaterial.HasProperty("_Tint"))
        {
            skyTintProperty = "_Tint";
        }

        if (skyTintProperty != null)
        {
            baseSkyTint = skyMaterial.GetColor(skyTintProperty);
        }
    }

    private void Update()
    {
        if (worldState == null || sunLight == null)
        {
            return;
        }

        float hour = worldState.HourFloat;

        // --- Posición del sol según la hora y la época del año ---
        // El año empieza en primavera; el solsticio de verano cae a mitad de verano (3/8 del año).
        float declination = axialTilt * Mathf.Sin((worldState.YearProgress - 0.125f) * Mathf.PI * 2f);
        float hourAngle = (hour - 12f) * 15f;

        Vector3 sunDirection = SkyDirection(hourAngle, declination, out float sunElevation);

        SunElevation = sunElevation;
        Daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-8f, 25f, sunElevation));

        // --- Clima y estación ---
        WeatherLook look = BlendLooks();
        Color seasonTint = GetSeasonTint(worldState.YearProgress);
        float flash = weatherEffects != null ? weatherEffects.LightningFlash : 0f;

        // --- Sol ---
        float sunIntensity = sunMaxIntensity
            * sunIntensityByElevation.Evaluate(sunElevation)
            * look.sunIntensity;

        Color sunColor = Desaturate(
            sunColorByElevation.Evaluate(Mathf.Clamp01(sunElevation / 40f)) * seasonTint,
            look.desaturation * 0.8f);

        // --- Lunas ---
        EvaluateMoons(
            hourAngle,
            declination,
            look.ambient,
            out Vector3 moonDirection,
            out float moonIntensity,
            out Color moonColor);

        // De día manda el sol; de noche, la luna más brillante. La intensidad
        // es continua en el cambio, así que no se nota el relevo.
        Vector3 lightDirection;
        float lightIntensity;
        Color lightColor;

        if (sunIntensity >= moonIntensity)
        {
            lightDirection = sunDirection;
            lightIntensity = sunIntensity;
        }
        else
        {
            lightDirection = moonDirection;
            lightIntensity = moonIntensity;
        }

        float sunShare = sunIntensity / (sunIntensity + moonIntensity + 0.0001f);
        lightColor = Color.Lerp(moonColor, sunColor, sunShare);

        // El rayo ilumina un instante todo
        lightIntensity += flash * 1.2f;

        ApplyLight(lightDirection, lightColor, lightIntensity, look.shadowStrength);

        // --- Luz ambiente ---
        float ambientMultiplier = ambientIntensity * look.ambient;
        Color moonAmbient = moonColor * (moonIntensity * 0.6f);
        Color flashAmbient = new Color(0.6f, 0.65f, 0.8f) * (flash * 0.8f);

        RenderSettings.ambientSkyColor =
            Desaturate(ambientSky.Evaluate(Daylight) * seasonTint, look.desaturation * 0.6f) * ambientMultiplier
            + moonAmbient + flashAmbient;

        RenderSettings.ambientEquatorColor =
            Desaturate(ambientEquator.Evaluate(Daylight) * seasonTint, look.desaturation * 0.6f) * ambientMultiplier
            + moonAmbient * 0.7f + flashAmbient;

        RenderSettings.ambientGroundColor =
            Desaturate(ambientGround.Evaluate(Daylight) * seasonTint, look.desaturation * 0.6f) * ambientMultiplier
            + moonAmbient * 0.3f;

        // --- Niebla ---
        RenderSettings.fogColor =
            Desaturate(fogColor.Evaluate(Daylight) * look.fogTint * seasonTint, look.desaturation * 0.5f)
            + flashAmbient * 0.5f;

        RenderSettings.fogDensity = look.fogDensity;

        // --- Cielo ---
        ApplySky(look, seasonTint, flash);

        firstFrame = false;
    }

    // ------------------------------------------------------------------
    // LUZ Y CIELO
    // ------------------------------------------------------------------
    private void ApplyLight(Vector3 towardsAstro, Color color, float intensity, float shadowStrength)
    {
        sunLight.color = color;
        sunLight.intensity = Mathf.Max(0f, intensity);
        sunLight.shadowStrength = shadowStrength;

        Quaternion target = LightRotation(towardsAstro);

        if (firstFrame)
        {
            sunLight.transform.rotation = target;
            return;
        }

        float blend = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
        sunLight.transform.rotation = Quaternion.Slerp(sunLight.transform.rotation, target, blend);
    }

    private void ApplySky(WeatherLook look, Color seasonTint, float flash)
    {
        if (skyMaterial == null)
        {
            return;
        }

        if (skyMaterial.HasProperty("_Exposure"))
        {
            float exposure = baseSkyExposure
                * look.skyExposure
                * Mathf.Lerp(nightSkyExposure, 1f, Daylight)
                * (1f + flash * 0.8f);

            skyMaterial.SetFloat("_Exposure", exposure);
        }

        if (skyTintProperty != null)
        {
            Color tint = Desaturate(baseSkyTint * seasonTint, look.desaturation);
            skyMaterial.SetColor(skyTintProperty, tint);
        }
    }

    // ------------------------------------------------------------------
    // LUNAS
    // ------------------------------------------------------------------
    private void EvaluateMoons(
        float sunHourAngle,
        float declination,
        float weatherMultiplier,
        out Vector3 bestDirection,
        out float bestIntensity,
        out Color bestColor)
    {
        bestDirection = Vector3.up;
        bestIntensity = 0f;
        bestColor = new Color(0.75f, 0.85f, 1f);

        if (moons == null)
        {
            return;
        }

        foreach (MoonSettings moon in moons)
        {
            if (moon == null)
            {
                continue;
            }

            // Fase del ciclo: 0 = luna nueva (junto al sol), 0,5 = luna llena (opuesta al sol)
            float phase = Mathf.Repeat(
                (float)(worldState.ContinuousDays / Mathf.Max(0.5f, moon.periodDays)) + moon.phaseOffset,
                1f);

            Vector3 direction = SkyDirection(sunHourAngle + phase * 360f, declination, out float elevation);

            float illumination = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            float visibility = Mathf.Clamp01((elevation + 3f) / 12f);
            float intensity = moon.maxIntensity * illumination * visibility * weatherMultiplier;

            if (intensity > bestIntensity)
            {
                bestIntensity = intensity;
                bestDirection = direction;
                bestColor = moon.color;
            }
        }
    }

    // ------------------------------------------------------------------
    // ASTRONOMÍA SIMPLIFICADA
    // ------------------------------------------------------------------

    /// <summary>
    /// Dirección HACIA un astro (en coordenadas del mundo) a partir de su ángulo horario
    /// (0 = mediodía, 15 grados por hora) y su declinación. Devuelve también su altura en grados.
    /// </summary>
    private Vector3 SkyDirection(float hourAngleDegrees, float declinationDegrees, out float elevationDegrees)
    {
        float lat = latitude * Mathf.Deg2Rad;
        float dec = declinationDegrees * Mathf.Deg2Rad;
        float h = hourAngleDegrees * Mathf.Deg2Rad;

        float sinAltitude = Mathf.Sin(lat) * Mathf.Sin(dec) + Mathf.Cos(lat) * Mathf.Cos(dec) * Mathf.Cos(h);
        float altitude = Mathf.Asin(Mathf.Clamp(sinAltitude, -1f, 1f));

        elevationDegrees = altitude * Mathf.Rad2Deg;

        // Azimut medido desde el norte hacia el este
        float azimuth = 0f;
        float denominator = Mathf.Cos(altitude) * Mathf.Cos(lat);

        if (Mathf.Abs(denominator) > 0.0001f)
        {
            float cosAzimuth = (Mathf.Sin(dec) - Mathf.Sin(altitude) * Mathf.Sin(lat)) / denominator;
            azimuth = Mathf.Acos(Mathf.Clamp(cosAzimuth, -1f, 1f));

            // Por la tarde (ángulo horario positivo) el astro está al oeste
            if (Mathf.Sin(h) > 0f)
            {
                azimuth = Mathf.PI * 2f - azimuth;
            }
        }

        // X = este, Y = arriba, Z = norte
        Vector3 direction = new Vector3(
            Mathf.Sin(azimuth) * Mathf.Cos(altitude),
            Mathf.Sin(altitude),
            Mathf.Cos(azimuth) * Mathf.Cos(altitude));

        return Quaternion.Euler(0f, northDirection, 0f) * direction;
    }

    private static Quaternion LightRotation(Vector3 towardsAstro)
    {
        // La luz apunta en sentido contrario a la dirección hacia el astro
        Vector3 forward = -towardsAstro;

        // Si apunta casi en vertical, usamos otro "arriba" para evitar un giro indefinido
        Vector3 up = Mathf.Abs(Vector3.Dot(forward.normalized, Vector3.up)) > 0.999f
            ? Vector3.forward
            : Vector3.up;

        return Quaternion.LookRotation(forward, up);
    }

    // ------------------------------------------------------------------
    // CLIMA Y ESTACIÓN
    // ------------------------------------------------------------------
    private WeatherLook BlendLooks()
    {
        WeatherLook result = new WeatherLook();
        Color fogTint = Color.black;
        float total = 0f;

        foreach (WeatherLook look in looks)
        {
            float weight = worldState.GetWeatherWeight(look.weather);

            if (weight <= 0f)
            {
                continue;
            }

            result.sunIntensity += look.sunIntensity * weight;
            result.ambient += look.ambient * weight;
            result.skyExposure += look.skyExposure * weight;
            result.fogDensity += look.fogDensity * weight;
            result.desaturation += look.desaturation * weight;
            result.shadowStrength += look.shadowStrength * weight;
            fogTint += look.fogTint * weight;
            total += weight;
        }

        if (total <= 0.0001f)
        {
            return looks[0];
        }

        float inverse = 1f / total;

        result.sunIntensity *= inverse;
        result.ambient *= inverse;
        result.skyExposure *= inverse;
        result.fogDensity *= inverse;
        result.desaturation *= inverse;
        result.shadowStrength *= inverse;
        result.fogTint = fogTint * inverse;

        return result;
    }

    // Mezcla suavemente el color de una estación con el de la siguiente
    private Color GetSeasonTint(float yearProgress)
    {
        if (seasonTints == null || seasonTints.Length == 0)
        {
            return Color.white;
        }

        int count = seasonTints.Length;

        // Cada color está en el centro de su estación
        float position = yearProgress * count - 0.5f;
        int index = Mathf.FloorToInt(position);
        float fraction = Mathf.SmoothStep(0f, 1f, position - index);

        Color from = seasonTints[((index % count) + count) % count];
        Color to = seasonTints[(((index + 1) % count) + count) % count];

        return Color.Lerp(from, to, fraction);
    }

    private static Color Desaturate(Color color, float amount)
    {
        float gray = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
        return Color.Lerp(color, new Color(gray, gray, gray, color.a), amount);
    }

    // ------------------------------------------------------------------
    // VALORES POR DEFECTO
    // ------------------------------------------------------------------
    private static WeatherLook[] DefaultLooks()
    {
        return new[]
        {
            new WeatherLook(WeatherType.Sunny,  1.00f, 1.00f, 1.00f, 0.0015f, new Color(1.00f, 1.00f, 1.00f), 0.00f, 1.00f),
            new WeatherLook(WeatherType.Cloudy, 0.55f, 0.90f, 0.80f, 0.0020f, new Color(0.90f, 0.92f, 0.95f), 0.35f, 0.50f),
            new WeatherLook(WeatherType.Rain,   0.30f, 0.75f, 0.60f, 0.0060f, new Color(0.75f, 0.80f, 0.85f), 0.60f, 0.25f),
            new WeatherLook(WeatherType.Storm,  0.15f, 0.55f, 0.40f, 0.0090f, new Color(0.55f, 0.60f, 0.68f), 0.75f, 0.10f),
            new WeatherLook(WeatherType.Snow,   0.50f, 1.00f, 0.85f, 0.0080f, new Color(0.95f, 0.97f, 1.00f), 0.50f, 0.35f),
            new WeatherLook(WeatherType.Fog,    0.35f, 0.85f, 0.70f, 0.0300f, new Color(0.85f, 0.88f, 0.90f), 0.60f, 0.20f),
            new WeatherLook(WeatherType.Wind,   0.90f, 1.00f, 0.95f, 0.0020f, new Color(1.00f, 1.00f, 1.00f), 0.10f, 0.85f)
        };
    }

    private static Gradient MakeGradient(params (float time, Color color)[] keys)
    {
        GradientColorKey[] colorKeys = new GradientColorKey[keys.Length];

        for (int i = 0; i < keys.Length; i++)
        {
            colorKeys[i] = new GradientColorKey(keys[i].color, keys[i].time);
        }

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            colorKeys,
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        return gradient;
    }

    private static AnimationCurve MakeCurve(params (float time, float value)[] keys)
    {
        AnimationCurve curve = new AnimationCurve();

        foreach ((float time, float value) in keys)
        {
            curve.AddKey(time, value);
        }

        for (int i = 0; i < curve.length; i++)
        {
            curve.SmoothTangents(i, 0f);
        }

        return curve;
    }
}
