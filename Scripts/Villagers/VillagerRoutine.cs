using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Cerebro básico de un aldeano. Elige una actividad (según personalidad,
/// hora, clima y azar), la ejecuta con un NavMeshAgent y vuelve a elegir.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(VillagerPersonality))]
public class VillagerRoutine : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Si se deja vacío, se busca automáticamente en la escena.")]
    [SerializeField] private WorldState worldState;

    [Header("Casa")]
    [Tooltip("Puerta o punto de entrada de su casa. Si se deja vacío, se usa el punto donde empieza.")]
    [SerializeField] private Transform homeSpot;

    [Tooltip("Oculta al aldeano mientras está dentro de casa.")]
    [SerializeField] private bool hideWhenInside = true;

    [Header("Movimiento")]
    [SerializeField] private float walkSpeed = 1.6f;
    [SerializeField] private float runSpeed = 2.8f;
    [SerializeField] private float turnSpeed = 240f;
    [SerializeField] private float wanderRadius = 8f;
    [SerializeField] private float circleRadius = 2.5f;
    [SerializeField] private float flowerDistance = 1.2f;

    [Header("Duración de las actividades (segundos: mín, máx)")]
    [SerializeField] private Vector2 idleTime = new Vector2(8f, 20f);
    [SerializeField] private Vector2 wanderTime = new Vector2(15f, 40f);
    [SerializeField] private Vector2 plazaTime = new Vector2(30f, 70f);
    [SerializeField] private Vector2 circlesTime = new Vector2(8f, 16f);
    [SerializeField] private Vector2 flowerTime = new Vector2(12f, 25f);
    [SerializeField] private Vector2 homeTime = new Vector2(20f, 45f);
    [SerializeField] private Vector2 shelterTime = new Vector2(25f, 60f);

    [Header("Depuración")]
    [SerializeField] private bool logDecisions = true;

    public ActivityType CurrentActivity { get; private set; } = ActivityType.Idle;
    public float ActivityRemaining => Mathf.Max(0f, activityRemaining);
    public bool IsPaused => paused;
    public bool IsInside { get; private set; }
    public bool HasHome => homeSpot != null;
    public Dictionary<ActivityType, float> LastWeights { get; private set; }

    private NavMeshAgent agent;
    private VillagerPersonality personality;
    private Renderer[] renderers;

    private Vector3 wanderCenter;
    private float activityRemaining;
    private ActivityType? lastActivity;
    private PointOfInterest lastFlower;

    // Conversación: el aldeano se para y mira al jugador
    private bool paused;
    private Transform lookTarget;

    // Para detectar cambios de hora o de clima y volver a decidir
    private DayPhase lastPhase;
    private WeatherType lastWeather;
    private bool needsReplan;

    // ------------------------------------------------------------------
    // CICLO DE VIDA
    // ------------------------------------------------------------------
    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        personality = GetComponent<VillagerPersonality>();
        renderers = GetComponentsInChildren<Renderer>();

        if (worldState == null)
        {
            worldState = FindFirstObjectByType<WorldState>();
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError(
                $"{name}: el aldeano no está sobre un NavMesh. ¿Has hecho el Bake?",
                this
            );
            enabled = false;
            return;
        }

        wanderCenter = transform.position;
        agent.stoppingDistance = 0f;

        if (worldState != null)
        {
            lastPhase = VillagerActivityPicker.GetPhase(personality, worldState.Hour);
            lastWeather = worldState.Weather;
        }

        StartCoroutine(Loop());
    }

    private void OnDisable()
    {
        SetInside(false);
    }

    private void Update()
    {
        if (!paused)
        {
            activityRemaining -= Time.deltaTime;
        }
        else if (lookTarget != null)
        {
            RotateTowards(lookTarget.position);
        }

        DetectContextChange();
    }

    // ------------------------------------------------------------------
    // API PÚBLICA
    // ------------------------------------------------------------------
    public void SetHome(Transform home)
    {
        homeSpot = home;
    }

    public void BeginConversation(Transform player)
    {
        paused = true;
        lookTarget = player;
    }

    public void EndConversation()
    {
        paused = false;
        lookTarget = null;
    }

    // ------------------------------------------------------------------
    // BUCLE PRINCIPAL
    // ------------------------------------------------------------------
    private IEnumerator Loop()
    {
        // Esperamos un frame para que el agente esté bien colocado
        yield return null;

        while (true)
        {
            // Si estaba dentro de casa, sale antes de decidir
            SetInside(false);

            ActivityType next = ChooseActivity();

            CurrentActivity = next;
            lastActivity = next;
            activityRemaining = GetDuration(next);

            yield return RunActivity(next);
        }
    }

    private ActivityType ChooseActivity()
    {
        Dictionary<ActivityType, float> weights =
            VillagerActivityPicker.CalculateWeights(personality, worldState, lastActivity);

        LastWeights = weights;
        ActivityType chosen = VillagerActivityPicker.PickWeighted(weights);

        if (logDecisions)
        {
            string context = worldState != null
                ? $"{worldState.Hour}h, {worldState.Weather}"
                : "sin WorldState";

            Debug.Log(
                $"[{personality.DisplayName}] {chosen}  ({context})\n" +
                VillagerActivityPicker.Describe(weights),
                this
            );
        }

        return chosen;
    }

    private IEnumerator RunActivity(ActivityType type)
    {
        switch (type)
        {
            case ActivityType.Wander: return WanderRoutine();
            case ActivityType.GoToPlaza: return PlazaRoutine();
            case ActivityType.RunInCircles: return CirclesRoutine();
            case ActivityType.ContemplateFlower: return FlowerRoutine();
            case ActivityType.GoHome: return GoHomeRoutine();
            case ActivityType.Sleep: return SleepRoutine();
            case ActivityType.SeekShelter: return ShelterRoutine();
            default: return IdleRoutine();
        }
    }

    private float GetDuration(ActivityType type)
    {
        switch (type)
        {
            case ActivityType.Wander: return Random.Range(wanderTime.x, wanderTime.y);
            case ActivityType.GoToPlaza: return Random.Range(plazaTime.x, plazaTime.y);
            case ActivityType.RunInCircles: return Random.Range(circlesTime.x, circlesTime.y);
            case ActivityType.ContemplateFlower: return Random.Range(flowerTime.x, flowerTime.y);
            case ActivityType.GoHome: return Random.Range(homeTime.x, homeTime.y);
            case ActivityType.SeekShelter: return Random.Range(shelterTime.x, shelterTime.y);
            case ActivityType.Sleep: return 0f; // dura hasta la hora de despertar
            default: return Random.Range(idleTime.x, idleTime.y);
        }
    }

    // ------------------------------------------------------------------
    // ACTIVIDADES
    // ------------------------------------------------------------------
    private IEnumerator IdleRoutine()
    {
        while (activityRemaining > 0f)
        {
            yield return null;
        }
    }

    private IEnumerator WanderRoutine()
    {
        float speed = WalkSpeed();

        while (activityRemaining > 0f)
        {
            if (TryGetRandomPoint(wanderCenter, wanderRadius, out Vector3 target))
            {
                yield return WalkTo(target, speed);
            }

            yield return WaitSeconds(Random.Range(1f, 4f));
        }
    }

    private IEnumerator PlazaRoutine()
    {
        PointOfInterest plaza = PointOfInterest.GetRandom(PointOfInterestType.Plaza);

        if (plaza == null)
        {
            Debug.LogWarning($"{name}: no hay ninguna plaza en la escena. Paseará en su lugar.", this);
            yield return WanderRoutine();
            yield break;
        }

        yield return WalkTo(plaza.GetRandomPoint(), WalkSpeed());

        // Una vez allí, se queda un rato
        activityRemaining = Random.Range(plazaTime.x, plazaTime.y);

        while (activityRemaining > 0f)
        {
            yield return null;
        }
    }

    private IEnumerator CirclesRoutine()
    {
        Vector3 center = transform.position;
        float direction = Random.value < 0.5f ? 1f : -1f;
        float angle = Random.Range(0f, 360f);
        float speed = RunSpeed();

        agent.autoBraking = false;

        while (activityRemaining > 0f)
        {
            angle += 45f * direction;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * circleRadius;

            if (NavMesh.SamplePosition(center + offset, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                yield return WalkTo(hit.position, speed, 0.7f, 5f);
            }
            else
            {
                yield return null;
            }
        }

        agent.autoBraking = true;
    }

    private IEnumerator FlowerRoutine()
    {
        PointOfInterest flower = PointOfInterest.GetRandom(PointOfInterestType.Flower, lastFlower);

        if (flower == null)
        {
            Debug.LogWarning($"{name}: no hay flores en la escena. Paseará en su lugar.", this);
            yield return WanderRoutine();
            yield break;
        }

        lastFlower = flower;

        // Se coloca a cierta distancia de la flor, por el lado por el que llega
        Vector3 away = transform.position - flower.transform.position;
        away.y = 0f;
        away = away.sqrMagnitude < 0.01f ? Vector3.forward : away.normalized;

        Vector3 standPoint = flower.transform.position + away * flowerDistance;

        if (NavMesh.SamplePosition(standPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            standPoint = hit.position;
        }

        yield return WalkTo(standPoint, WalkSpeed());
        yield return FaceTowards(flower.transform.position);

        activityRemaining = Random.Range(flowerTime.x, flowerTime.y);

        while (activityRemaining > 0f)
        {
            yield return null;
        }
    }

    // Va a casa, entra y se queda un rato
    private IEnumerator GoHomeRoutine()
    {
        yield return EnterHome(WalkSpeed());

        activityRemaining = Random.Range(homeTime.x, homeTime.y);

        while (activityRemaining > 0f)
        {
            yield return null;
        }
    }

    // Va a casa, entra y duerme hasta que le toque despertar
    private IEnumerator SleepRoutine()
    {
        yield return EnterHome(WalkSpeed());

        while (worldState != null && personality.IsAsleepAt(worldState.Hour))
        {
            yield return null;
        }
    }

    // Se resguarda de la lluvia: en el refugio más cercano o, si no hay, en casa
    private IEnumerator ShelterRoutine()
    {
        PointOfInterest shelter =
            PointOfInterest.GetNearest(PointOfInterestType.Shelter, transform.position);

        if (shelter != null)
        {
            yield return WalkTo(shelter.GetRandomPoint(), HurrySpeed(), 0.3f, 90f);
        }
        else
        {
            yield return EnterHome(HurrySpeed());
        }

        activityRemaining = Random.Range(shelterTime.x, shelterTime.y);

        while (activityRemaining > 0f)
        {
            yield return null;
        }
    }

    // Camina hasta la puerta y entra (se oculta, si está activado)
    private IEnumerator EnterHome(float speed)
    {
        Vector3 door = HomePoint;

        yield return WalkTo(door, speed, 0.3f, 90f);

        // Si se atascó por el camino, lo colocamos en la puerta
        Vector3 delta = transform.position - door;
        delta.y = 0f;

        if (delta.sqrMagnitude > 4f && agent.isOnNavMesh)
        {
            agent.Warp(door);
        }

        SetInside(true);
    }

    // ------------------------------------------------------------------
    // MOVIMIENTO
    // ------------------------------------------------------------------
    private IEnumerator WalkTo(Vector3 target, float speed, float reachDistance = 0.3f, float timeout = 30f)
    {
        agent.speed = speed;
        agent.SetDestination(target);

        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (paused)
            {
                agent.isStopped = true;
                yield return null;
                continue;
            }

            agent.isStopped = false;
            elapsed += Time.deltaTime;

            if (!agent.pathPending)
            {
                if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                {
                    yield break;
                }

                if (agent.remainingDistance <= reachDistance)
                {
                    yield break;
                }
            }

            yield return null;
        }
    }

    private IEnumerator WaitSeconds(float seconds)
    {
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            if (!paused)
            {
                elapsed += Time.deltaTime;
            }

            yield return null;
        }
    }

    private IEnumerator FaceTowards(Vector3 worldPoint)
    {
        while (true)
        {
            if (!paused && RotateTowards(worldPoint))
            {
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>Gira hacia un punto. Devuelve true cuando ya está mirando hacia él.</summary>
    private bool RotateTowards(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        Quaternion target = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);

        return Quaternion.Angle(transform.rotation, target) < 3f;
    }

    private bool TryGetRandomPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        result = center;
        return false;
    }

    // Punto de la puerta de casa, sobre el NavMesh
    private Vector3 HomePoint
    {
        get
        {
            Vector3 position = homeSpot != null ? homeSpot.position : wanderCenter;

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return position;
        }
    }

    private void SetInside(bool inside)
    {
        if (IsInside == inside)
        {
            return;
        }

        IsInside = inside;

        if (hideWhenInside && renderers != null)
        {
            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    r.enabled = !inside;
                }
            }
        }
    }

    // Los energéticos van algo más rápido y los tranquilos algo más despacio
    private float SpeedMultiplier()
    {
        return personality.energy == EnergyTrait.Energico ? 1.25f : 0.85f;
    }

    private float WalkSpeed() => walkSpeed * SpeedMultiplier();
    private float RunSpeed() => runSpeed * SpeedMultiplier();

    // Trote para huir de la lluvia: a medio camino entre caminar y correr
    private float HurrySpeed() => Mathf.Lerp(walkSpeed, runSpeed, 0.5f) * SpeedMultiplier();

    // ------------------------------------------------------------------
    // CAMBIOS DE HORA O DE CLIMA
    // ------------------------------------------------------------------
    private void DetectContextChange()
    {
        if (worldState == null)
        {
            return;
        }

        DayPhase phase = VillagerActivityPicker.GetPhase(personality, worldState.Hour);
        WeatherType weather = worldState.Weather;

        if (phase != lastPhase || weather != lastWeather)
        {
            needsReplan = true;
            lastPhase = phase;
            lastWeather = weather;
        }

        // Si está hablando, esperamos a que termine
        if (needsReplan && !paused)
        {
            needsReplan = false;
            Replan();
        }
    }

    private void Replan()
    {
        StopAllCoroutines();
        SetInside(false);

        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = false;
        }

        agent.autoBraking = true;
        StartCoroutine(Loop());
    }
}