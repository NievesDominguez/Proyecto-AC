using System;
using UnityEngine;
using Yarn.Unity;
using Random = UnityEngine.Random;

public enum WeatherType
{
    Sunny,
    Cloudy,
    Rain,
    Storm,
    Snow,
    Fog,
    Wind
}

public enum Season
{
    Spring,
    Summer,
    Autumn,
    Winter
}

public enum ClockMode
{
    RealTime,       // Hora y fecha reales del sistema
    Fixed,          // Hora fija (la del Inspector); el clima sigue evolucionando
    Accelerated     // El tiempo pasa rápido (para probar días y estaciones enteras)
}

/// <summary>
/// Estado del mundo: hora, calendario del planeta (estaciones), clima y evento activo.
/// Es la fuente de verdad que leen la iluminación, los efectos del clima,
/// los aldeanos y los diálogos.
/// </summary>
public class WorldState : MonoBehaviour
{
    private static readonly int WeatherCount = Enum.GetValues(typeof(WeatherType)).Length;

    // Probabilidad base de cada clima según la estación.
    // Filas: estación. Columnas: Sunny, Cloudy, Rain, Storm, Snow, Fog, Wind
    private static readonly float[,] SeasonWeights =
    {
        { 3.0f, 3.0f, 3.0f, 1.0f, 0.1f, 1.0f, 1.5f },   // Primavera
        { 5.0f, 2.0f, 1.5f, 1.5f, 0.0f, 0.5f, 1.0f },   // Verano
        { 2.0f, 3.0f, 3.5f, 1.5f, 0.2f, 2.0f, 2.5f },   // Otoño
        { 2.0f, 3.0f, 1.0f, 0.5f, 4.0f, 1.5f, 1.5f }    // Invierno
    };

    // Lo probable que es pasar de un clima (fila) a otro (columna).
    // Evita saltos poco naturales (de sol radiante a tormenta, por ejemplo).
    // Orden: Sunny, Cloudy, Rain, Storm, Snow, Fog, Wind
    private static readonly float[,] Affinity =
    {
        { 0.0f, 3.0f, 1.0f, 0.1f, 0.5f, 1.0f, 2.0f },   // desde Sunny
        { 3.0f, 0.0f, 3.0f, 0.7f, 1.5f, 1.5f, 2.0f },   // desde Cloudy
        { 0.7f, 3.0f, 0.0f, 2.0f, 0.5f, 1.5f, 1.0f },   // desde Rain
        { 0.1f, 2.0f, 3.0f, 0.0f, 0.1f, 0.3f, 1.0f },   // desde Storm
        { 0.5f, 3.0f, 0.5f, 0.1f, 0.0f, 2.0f, 1.0f },   // desde Snow
        { 3.0f, 3.0f, 1.0f, 0.1f, 0.5f, 0.0f, 0.5f },   // desde Fog
        { 3.0f, 3.0f, 1.0f, 1.0f, 0.3f, 0.5f, 0.0f }    // desde Wind
    };

    [Header("Reloj")]
    [SerializeField] private ClockMode clockMode = ClockMode.RealTime;

    [Tooltip("Hora (0-24) usada en modo Fixed, y hora de inicio en modo Accelerated.")]
    [SerializeField, Range(0f, 24f)] private float startHour = 12f;

    [Tooltip("En modo Accelerated: horas de juego que pasan por cada segundo real (0,1 = un día en 4 minutos).")]
    [SerializeField, Min(0f)] private float acceleratedHoursPerSecond = 0.1f;

    [Header("Calendario del planeta")]
    [Tooltip("Días por estación. El año tiene cuatro estaciones (por defecto 4 x 42 = 168 días).")]
    [SerializeField, Min(1)] private int daysPerSeason = 42;

    [Tooltip("Fecha real en la que empieza el año del planeta (el primer día de la primavera).")]
    [SerializeField] private int epochYear = 2026;
    [SerializeField] private int epochMonth = 1;
    [SerializeField] private int epochDay = 1;

    [Tooltip("Fuerza un día del año (0 = primer día de primavera). -1 = usar la fecha real.")]
    [SerializeField] private int debugDayOfYear = -1;

    [Header("Clima")]
    [Tooltip("Clima actual. Si el clima aleatorio está activo, el sistema lo va cambiando solo; " +
             "si lo cambias aquí a mano, se hace una transición suave hacia él.")]
    [SerializeField] private WeatherType weather = WeatherType.Sunny;

    [SerializeField] private bool randomWeather = true;

    [Tooltip("Duración mínima de cada clima, en horas de juego.")]
    [SerializeField, Min(0.5f)] private float minWeatherHours = 2f;

    [Tooltip("Duración máxima de cada clima, en horas de juego.")]
    [SerializeField, Min(1f)] private float maxWeatherHours = 5f;

    [Tooltip("Segundos reales que dura la transición visual entre dos climas.")]
    [SerializeField, Min(1f)] private float transitionSeconds = 45f;

    [Header("Evento activo (vacío = ninguno)")]
    [SerializeField] private string currentEvent = "";

    [Header("Depuración")]
    [SerializeField] private bool showDebugOverlay = true;

    // Estado interno del clima
    private float[] weatherWeights;
    private WeatherType lastSeenWeather;
    private float hoursUntilWeatherChange;
    private bool initialized;

    // Estado interno del reloj
    private double acceleratedHours;

    private GUIStyle overlayStyle;

    // ------------------------------------------------------------------
    // PROPIEDADES PÚBLICAS
    // ------------------------------------------------------------------

    /// <summary>Hora del día con decimales (0 a 24).</summary>
    public float HourFloat { get; private set; }

    /// <summary>Hora actual (0-23).</summary>
    public int Hour => Mathf.Clamp(Mathf.FloorToInt(HourFloat), 0, 23);

    /// <summary>Días transcurridos desde el inicio del calendario del planeta.</summary>
    public int AbsoluteDay { get; private set; }

    public int YearLengthDays => daysPerSeason * 4;

    /// <summary>Día del año (0 = primer día de primavera).</summary>
    public int DayOfYear => PositiveMod(AbsoluteDay, YearLengthDays);

    /// <summary>Avance del año, de 0 a 1.</summary>
    public float YearProgress => (DayOfYear + HourFloat / 24f) / YearLengthDays;

    public Season Season => (Season)Mathf.Clamp(DayOfYear / Mathf.Max(1, daysPerSeason), 0, 3);

    /// <summary>Tiempo continuo en días (sirve para las fases de las lunas).</summary>
    public double ContinuousDays => AbsoluteDay + HourFloat / 24.0;

    /// <summary>Clima actual (el que se está alcanzando).</summary>
    public WeatherType Weather => weather;

    public float HoursUntilWeatherChange => hoursUntilWeatherChange;

    public string CurrentEvent => currentEvent;
    public bool HasActiveEvent => !string.IsNullOrEmpty(currentEvent);

    public string TimeOfDay
    {
        get
        {
            int hour = Hour;

            if (hour >= 6 && hour < 12) return "morning";
            if (hour >= 12 && hour < 20) return "afternoon";
            return "night";
        }
    }

    /// <summary>
    /// Cuánto pesa cada clima ahora mismo (0 a 1; todos suman 1).
    /// Durante una transición, el clima anterior baja y el nuevo sube poco a poco.
    /// </summary>
    public float GetWeatherWeight(WeatherType type)
    {
        EnsureInitialized();

        float total = 0f;

        for (int i = 0; i < weatherWeights.Length; i++)
        {
            total += weatherWeights[i];
        }

        if (total <= 0.0001f)
        {
            return type == weather ? 1f : 0f;
        }

        return weatherWeights[(int)type] / total;
    }

    // ------------------------------------------------------------------
    // API PARA OTROS SISTEMAS
    // ------------------------------------------------------------------
    public void SetWeather(WeatherType value)
    {
        weather = value;
    }

    /// <summary>Compatibilidad: -1 vuelve a la hora real; 0-23 fija esa hora.</summary>
    public void SetDebugHour(int hour)
    {
        if (hour < 0)
        {
            clockMode = ClockMode.RealTime;
            return;
        }

        clockMode = ClockMode.Fixed;
        startHour = Mathf.Clamp(hour, 0, 23);
    }

    public void SetClockMode(ClockMode mode)
    {
        clockMode = mode;
    }

    public void SetEvent(string eventName)
    {
        currentEvent = eventName ?? "";
    }

    public void ClearEvent()
    {
        currentEvent = "";
    }

    /// <summary>
    /// Escribe el estado actual en las variables de Yarn.
    /// Los nombres deben coincidir con los de Globals.yarn.
    /// </summary>
    public void PushTo(VariableStorageBehaviour variables)
    {
        variables.SetValue("$weather", WeatherToYarn(weather));
        variables.SetValue("$time_of_day", TimeOfDay);
        variables.SetValue("$event", HasActiveEvent ? currentEvent : "none");
    }

    // ------------------------------------------------------------------
    // CICLO DE VIDA
    // ------------------------------------------------------------------
    private void Awake()
    {
        EnsureInitialized();
    }

    private void Update()
    {
        EnsureInitialized();

        float dt = Time.deltaTime;

        UpdateClock(dt);
        UpdateWeather(dt);
    }

    private void OnValidate()
    {
        if (maxWeatherHours < minWeatherHours)
        {
            maxWeatherHours = minWeatherHours;
        }

        epochMonth = Mathf.Clamp(epochMonth, 1, 12);
        epochYear = Mathf.Clamp(epochYear, 1, 9999);
        epochDay = Mathf.Clamp(epochDay, 1, DateTime.DaysInMonth(epochYear, epochMonth));
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        weatherWeights = new float[WeatherCount];

        // El reloj primero: la elección del clima inicial depende de la estación y la hora
        UpdateClock(0f);

        if (randomWeather)
        {
            weather = PickWeather(null);
        }

        weatherWeights[(int)weather] = 1f;
        lastSeenWeather = weather;
        hoursUntilWeatherChange = Random.Range(minWeatherHours, maxWeatherHours);
    }

    // ------------------------------------------------------------------
    // RELOJ Y CALENDARIO
    // ------------------------------------------------------------------
    private void UpdateClock(float deltaSeconds)
    {
        switch (clockMode)
        {
            case ClockMode.RealTime:
            {
                DateTime now = DateTime.Now;
                HourFloat = (float)now.TimeOfDay.TotalHours;
                AbsoluteDay = DaysSinceEpoch(now);
                break;
            }

            case ClockMode.Fixed:
            {
                HourFloat = Mathf.Repeat(startHour, 24f);
                AbsoluteDay = debugDayOfYear >= 0 ? debugDayOfYear : DaysSinceEpoch(DateTime.Now);
                break;
            }

            case ClockMode.Accelerated:
            {
                acceleratedHours += deltaSeconds * acceleratedHoursPerSecond;

                double total = startHour + acceleratedHours;
                HourFloat = (float)(total % 24.0);

                int baseDay = debugDayOfYear >= 0 ? debugDayOfYear : DaysSinceEpoch(DateTime.Now);
                AbsoluteDay = baseDay + (int)Math.Floor(total / 24.0);
                break;
            }
        }
    }

    private DateTime EpochDate()
    {
        int year = Mathf.Clamp(epochYear, 1, 9999);
        int month = Mathf.Clamp(epochMonth, 1, 12);
        int day = Mathf.Clamp(epochDay, 1, DateTime.DaysInMonth(year, month));

        return new DateTime(year, month, day);
    }

    private int DaysSinceEpoch(DateTime now)
    {
        return (int)(now.Date - EpochDate()).TotalDays;
    }

    private static int PositiveMod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    // ------------------------------------------------------------------
    // CLIMA
    // ------------------------------------------------------------------
    private void UpdateWeather(float deltaSeconds)
    {
        // Si se cambió el clima a mano (Inspector o SetWeather), se queda un rato normal
        if (weather != lastSeenWeather)
        {
            lastSeenWeather = weather;
            hoursUntilWeatherChange = Random.Range(minWeatherHours, maxWeatherHours);
        }

        if (randomWeather)
        {
            float hoursPerSecond = clockMode == ClockMode.Accelerated
                ? acceleratedHoursPerSecond
                : 1f / 3600f;

            hoursUntilWeatherChange -= deltaSeconds * hoursPerSecond;

            if (hoursUntilWeatherChange <= 0f)
            {
                weather = PickWeather(weather);
                lastSeenWeather = weather;
                hoursUntilWeatherChange = Random.Range(minWeatherHours, maxWeatherHours);
            }
        }

        // Cada peso se acerca poco a poco a su objetivo: el clima nuevo sube y los demás bajan.
        // Si llega otro cambio a mitad de una transición, no hay saltos.
        float step = deltaSeconds / Mathf.Max(0.1f, transitionSeconds);

        for (int i = 0; i < weatherWeights.Length; i++)
        {
            float target = i == (int)weather ? 1f : 0f;
            weatherWeights[i] = Mathf.MoveTowards(weatherWeights[i], target, step);
        }
    }

    // Elige un clima al azar según la estación, la hora y el clima anterior.
    // Con 'current' nulo (inicio), no se tiene en cuenta el anterior.
    private WeatherType PickWeather(WeatherType? current)
    {
        float[] chances = new float[WeatherCount];
        float total = 0f;
        int seasonIndex = (int)Season;

        for (int i = 0; i < WeatherCount; i++)
        {
            WeatherType candidate = (WeatherType)i;

            float chance = SeasonWeights[seasonIndex, i] * TimeFactor(candidate, HourFloat);

            if (current.HasValue)
            {
                chance *= Affinity[(int)current.Value, i];
            }

            chances[i] = chance;
            total += chance;
        }

        if (total <= 0f)
        {
            return current == WeatherType.Sunny ? WeatherType.Cloudy : WeatherType.Sunny;
        }

        float roll = Random.value * total;

        for (int i = 0; i < WeatherCount; i++)
        {
            roll -= chances[i];

            if (roll <= 0f && chances[i] > 0f)
            {
                return (WeatherType)i;
            }
        }

        return WeatherType.Cloudy;
    }

    // Algunos climas son más típicos de ciertas horas
    private static float TimeFactor(WeatherType type, float hour)
    {
        switch (type)
        {
            case WeatherType.Fog:
                return hour >= 4f && hour < 10f ? 3f : 0.6f;

            case WeatherType.Storm:
                return hour >= 13f && hour < 20f ? 1.5f : 0.7f;

            default:
                return 1f;
        }
    }

    private static string WeatherToYarn(WeatherType value)
    {
        switch (value)
        {
            case WeatherType.Sunny: return "sunny";
            case WeatherType.Cloudy: return "cloudy";
            case WeatherType.Rain: return "rain";
            case WeatherType.Storm: return "storm";
            case WeatherType.Snow: return "snow";
            case WeatherType.Fog: return "fog";
            case WeatherType.Wind: return "wind";
            default: return "sunny";
        }
    }

    // ------------------------------------------------------------------
    // DEPURACIÓN
    // ------------------------------------------------------------------
    private void OnGUI()
    {
        if (!showDebugOverlay)
        {
            return;
        }

        if (overlayStyle == null)
        {
            overlayStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            overlayStyle.normal.textColor = Color.white;
        }

        int hour = Hour;
        int minutes = Mathf.FloorToInt((HourFloat - hour) * 60f);

        string text =
            $"{hour:00}:{minutes:00}   día {DayOfYear + 1}/{YearLengthDays}   ({Season})\n" +
            $"Clima: {weather}   (cambio en {Mathf.Max(0f, hoursUntilWeatherChange):0.0} h de juego)";

        Rect rect = new Rect(10f, 10f, 520f, 44f);

        GUI.color = Color.black;
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, overlayStyle);
        GUI.color = Color.white;
        GUI.Label(rect, text, overlayStyle);
    }
}