using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum ActivityType
{
    Idle,               // Quedarse quieto descansando
    Wander,             // Pasear
    GoToPlaza,          // Ir a la plaza y quedarse
    RunInCircles,       // Correr en círculos
    ContemplateFlower,  // Contemplar una flor
    GoHome,             // Ir a casa y quedarse un rato
    Sleep,              // Ir a casa y dormir hasta la hora de despertar
    SeekShelter         // Resguardarse de la lluvia
}

public enum DayPhase
{
    Morning,
    Afternoon,
    Night
}

/// <summary>
/// Calcula cuánto le apetece a un aldeano cada actividad.
/// Peso final = peso base x efecto de la personalidad x efecto de la hora x efecto del clima.
/// Después se elige al azar según esos pesos.
///
/// Excepción: cuando le toca dormir según su horario, la única opción es Sleep.
///
/// Para ajustar el comportamiento basta con cambiar los números de este archivo.
/// </summary>
public static class VillagerActivityPicker
{
    private static readonly ActivityType[] AllActivities =
        (ActivityType[])System.Enum.GetValues(typeof(ActivityType));

    /// <summary>Fase del día para ESTE aldeano, según su horario de sueño.</summary>
    public static DayPhase GetPhase(VillagerPersonality p, int hour)
    {
        if (p.IsAsleepAt(hour))
        {
            return DayPhase.Night;
        }

        // Las primeras horas tras despertar cuentan como "mañana"
        return p.HoursSinceWake(hour) < 5 ? DayPhase.Morning : DayPhase.Afternoon;
    }

    public static Dictionary<ActivityType, float> CalculateWeights(
        VillagerPersonality p,
        WorldState world,
        ActivityType? last)
    {
        // Si le toca dormir, solo puede dormir
        if (world != null && p.IsAsleepAt(world.Hour))
        {
            Dictionary<ActivityType, float> sleepOnly = new Dictionary<ActivityType, float>();

            foreach (ActivityType a in AllActivities)
            {
                sleepOnly[a] = a == ActivityType.Sleep ? 1f : 0f;
            }

            return sleepOnly;
        }

        // Pesos base
        Dictionary<ActivityType, float> w = new Dictionary<ActivityType, float>
        {
            { ActivityType.Idle, 0.4f },
            { ActivityType.Wander, 1.0f },
            { ActivityType.GoToPlaza, 1.0f },
            { ActivityType.RunInCircles, 0.5f },
            { ActivityType.ContemplateFlower, 0.6f },
            { ActivityType.GoHome, 0.3f },
            { ActivityType.Sleep, 0.0f },        // solo por horario
            { ActivityType.SeekShelter, 1.0f }   // el clima lo anula si no llueve
        };

        ApplyPersonality(w, p);

        if (world != null)
        {
            ApplyPhase(w, GetPhase(p, world.Hour));
            ApplyBedtime(w, p, world.Hour);
            ApplyWeather(w, p, world.Weather);
        }
        else
        {
            // Sin información de clima, nunca busca refugio
            Mul(w, ActivityType.SeekShelter, 0f);
        }

        // Evita repetir la misma actividad dos veces seguidas
        if (last.HasValue)
        {
            Mul(w, last.Value, 0.3f);
        }

        return w;
    }

    public static ActivityType PickWeighted(Dictionary<ActivityType, float> weights)
    {
        float total = 0f;

        foreach (ActivityType a in AllActivities)
        {
            total += weights[a];
        }

        if (total <= 0f)
        {
            return ActivityType.Idle;
        }

        float roll = Random.value * total;

        foreach (ActivityType a in AllActivities)
        {
            roll -= weights[a];

            if (roll <= 0f && weights[a] > 0f)
            {
                return a;
            }
        }

        return ActivityType.Idle;
    }

    public static string Describe(Dictionary<ActivityType, float> weights)
    {
        StringBuilder sb = new StringBuilder();

        foreach (ActivityType a in AllActivities)
        {
            sb.Append(a).Append('=').Append(weights[a].ToString("0.00")).Append("  ");
        }

        return sb.ToString().TrimEnd();
    }

    // ------------------------------------------------------------------
    // PERSONALIDAD
    // ------------------------------------------------------------------
    private static void ApplyPersonality(Dictionary<ActivityType, float> w, VillagerPersonality p)
    {
        // Social
        if (p.social == SocialTrait.Extrovertido)
        {
            Mul(w, ActivityType.GoToPlaza, 2.0f);
            Mul(w, ActivityType.Wander, 1.1f);
            Mul(w, ActivityType.Idle, 0.7f);
            Mul(w, ActivityType.GoHome, 0.6f);
        }
        else
        {
            Mul(w, ActivityType.GoToPlaza, 0.5f);
            Mul(w, ActivityType.ContemplateFlower, 1.5f);
            Mul(w, ActivityType.Idle, 1.3f);
            Mul(w, ActivityType.GoHome, 1.5f);
        }

        // Energía
        if (p.energy == EnergyTrait.Energico)
        {
            Mul(w, ActivityType.RunInCircles, 2.5f);
            Mul(w, ActivityType.Wander, 1.3f);
            Mul(w, ActivityType.Idle, 0.5f);
            Mul(w, ActivityType.ContemplateFlower, 0.6f);
            Mul(w, ActivityType.GoHome, 0.7f);
        }
        else
        {
            Mul(w, ActivityType.RunInCircles, 0.3f);
            Mul(w, ActivityType.ContemplateFlower, 1.6f);
            Mul(w, ActivityType.Idle, 1.4f);
            Mul(w, ActivityType.Wander, 0.9f);
            Mul(w, ActivityType.GoHome, 1.2f);
        }

        // Perspectiva
        if (p.outlook == OutlookTrait.Optimista)
        {
            Mul(w, ActivityType.Wander, 1.2f);
            Mul(w, ActivityType.GoToPlaza, 1.2f);
            Mul(w, ActivityType.RunInCircles, 1.3f);
        }
        else
        {
            Mul(w, ActivityType.Idle, 1.3f);
            Mul(w, ActivityType.RunInCircles, 0.7f);
            Mul(w, ActivityType.GoHome, 1.3f);
        }

        // Decisión
        if (p.decision == DecisionTrait.Emocional)
        {
            Mul(w, ActivityType.ContemplateFlower, 1.4f);
            Mul(w, ActivityType.GoToPlaza, 1.1f);
        }
        else
        {
            Mul(w, ActivityType.Wander, 1.1f);
            Mul(w, ActivityType.ContemplateFlower, 0.8f);
        }

        // Confianza
        if (p.trust == TrustTrait.Confiado)
        {
            Mul(w, ActivityType.GoToPlaza, 1.3f);
            Mul(w, ActivityType.Wander, 1.1f);
        }
        else
        {
            Mul(w, ActivityType.GoToPlaza, 0.8f);
            Mul(w, ActivityType.Idle, 1.2f);
            Mul(w, ActivityType.GoHome, 1.2f);
        }

        // Peculiaridades (de momento solo estas)
        if (p.peculiarity == Peculiarity.Sonador)
        {
            Mul(w, ActivityType.ContemplateFlower, 2.0f);
        }
    }

    // ------------------------------------------------------------------
    // HORA DEL DÍA
    // ------------------------------------------------------------------
    private static void ApplyPhase(Dictionary<ActivityType, float> w, DayPhase phase)
    {
        switch (phase)
        {
            case DayPhase.Morning:
                Mul(w, ActivityType.Wander, 1.2f);
                Mul(w, ActivityType.ContemplateFlower, 1.4f);
                Mul(w, ActivityType.GoToPlaza, 0.9f);
                Mul(w, ActivityType.RunInCircles, 0.9f);
                Mul(w, ActivityType.Idle, 0.8f);
                Mul(w, ActivityType.GoHome, 0.5f);
                break;

            case DayPhase.Afternoon:
                Mul(w, ActivityType.GoToPlaza, 1.6f);
                Mul(w, ActivityType.RunInCircles, 1.2f);
                Mul(w, ActivityType.Idle, 0.7f);
                Mul(w, ActivityType.GoHome, 0.8f);
                break;

            case DayPhase.Night:
                // No se usa: de noche el aldeano duerme (ver CalculateWeights)
                break;
        }
    }

    // En las dos horas previas a acostarse, tiende a volver a casa
    private static void ApplyBedtime(Dictionary<ActivityType, float> w, VillagerPersonality p, int hour)
    {
        int hoursToBed = (p.BedtimeHour - hour + 24) % 24;

        if (hoursToBed <= 2)
        {
            Mul(w, ActivityType.GoHome, 5.0f);
            Mul(w, ActivityType.RunInCircles, 0.5f);
        }
    }

    // ------------------------------------------------------------------
    // CLIMA
    // ------------------------------------------------------------------
    private static void ApplyWeather(Dictionary<ActivityType, float> w, VillagerPersonality p, WeatherType weather)
    {
        // Solo busca refugio cuando llueve o hay tormenta
        if (weather != WeatherType.Rain && weather != WeatherType.Storm)
        {
            Mul(w, ActivityType.SeekShelter, 0f);
        }

        switch (weather)
        {
            case WeatherType.Sunny:
                Mul(w, ActivityType.Wander, 1.2f);
                Mul(w, ActivityType.ContemplateFlower, 1.3f);
                Mul(w, ActivityType.GoToPlaza, 1.2f);
                break;

            case WeatherType.Rain:
                Mul(w, ActivityType.Wander, 0.4f);
                Mul(w, ActivityType.GoToPlaza, 0.4f);
                Mul(w, ActivityType.RunInCircles, 0.4f);
                Mul(w, ActivityType.ContemplateFlower, 0.3f);
                Mul(w, ActivityType.Idle, 1.2f);
                Mul(w, ActivityType.SeekShelter, 3.0f);
                Mul(w, ActivityType.GoHome, 1.5f);

                // Algunos disfrutan la lluvia y se resguardan menos
                if (p.energy == EnergyTrait.Energico)
                {
                    Mul(w, ActivityType.RunInCircles, 3.0f);
                    Mul(w, ActivityType.SeekShelter, 0.4f);
                }

                if (p.outlook == OutlookTrait.Optimista)
                {
                    Mul(w, ActivityType.Wander, 1.8f);
                    Mul(w, ActivityType.SeekShelter, 0.6f);
                }

                if (p.social == SocialTrait.Introvertido)
                {
                    Mul(w, ActivityType.ContemplateFlower, 1.5f);
                    Mul(w, ActivityType.SeekShelter, 1.3f);
                }
                break;

            case WeatherType.Storm:
                Mul(w, ActivityType.Wander, 0.1f);
                Mul(w, ActivityType.GoToPlaza, 0.1f);
                Mul(w, ActivityType.RunInCircles, 0.05f);
                Mul(w, ActivityType.ContemplateFlower, 0.05f);
                Mul(w, ActivityType.Idle, 2.0f);
                Mul(w, ActivityType.SeekShelter, 8.0f);
                Mul(w, ActivityType.GoHome, 3.0f);

                if (p.outlook == OutlookTrait.Pesimista)
                {
                    Mul(w, ActivityType.SeekShelter, 1.5f);
                }
                break;

            case WeatherType.Snow:
                Mul(w, ActivityType.Wander, 0.7f);
                Mul(w, ActivityType.GoToPlaza, 0.7f);
                Mul(w, ActivityType.RunInCircles, 1.3f);
                Mul(w, ActivityType.ContemplateFlower, 0.2f);
                Mul(w, ActivityType.Idle, 1.2f);

                if (p.outlook == OutlookTrait.Optimista)
                {
                    Mul(w, ActivityType.Wander, 1.8f);
                }
                break;

            case WeatherType.Fog:
                Mul(w, ActivityType.Wander, 0.6f);
                Mul(w, ActivityType.GoToPlaza, 0.8f);
                Mul(w, ActivityType.RunInCircles, 0.5f);
                Mul(w, ActivityType.ContemplateFlower, 0.8f);
                Mul(w, ActivityType.Idle, 1.5f);
                break;

            case WeatherType.Wind:
                Mul(w, ActivityType.Wander, 0.8f);
                Mul(w, ActivityType.RunInCircles, 0.7f);
                Mul(w, ActivityType.ContemplateFlower, 0.5f);
                Mul(w, ActivityType.Idle, 1.2f);
                break;

            case WeatherType.Cloudy:
            default:
                break;
        }
    }

    private static void Mul(Dictionary<ActivityType, float> w, ActivityType type, float factor)
    {
        w[type] *= factor;
    }
}