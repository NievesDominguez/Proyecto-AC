using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum PointOfInterestType
{
    Plaza,
    Flower,
    Shelter
}

/// <summary>
/// Marca un lugar del mundo al que los aldeanos pueden ir:
/// una plaza, una flor que contemplar o un refugio contra la lluvia.
/// </summary>
public class PointOfInterest : MonoBehaviour
{
    private static readonly List<PointOfInterest> all = new List<PointOfInterest>();

    [SerializeField] private PointOfInterestType type = PointOfInterestType.Flower;

    [Tooltip("Radio de la zona. 0 = punto exacto (para flores). En plazas y refugios, el área donde se reparten.")]
    [SerializeField] private float radius = 0f;

    public PointOfInterestType Type => type;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
    }

    private void OnEnable()
    {
        if (!all.Contains(this))
        {
            all.Add(this);
        }
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    /// <summary>Para configurarlo por código (lo usa la herramienta de la escena de prueba).</summary>
    public void Configure(PointOfInterestType newType, float newRadius)
    {
        type = newType;
        radius = newRadius;
    }

    /// <summary>Un punto aleatorio dentro de la zona, sobre el NavMesh.</summary>
    public Vector3 GetRandomPoint()
    {
        if (radius <= 0f)
        {
            return transform.position;
        }

        for (int i = 0; i < 10; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        return transform.position;
    }

    /// <summary>Devuelve un punto de interés aleatorio de ese tipo (o null si no hay).</summary>
    public static PointOfInterest GetRandom(PointOfInterestType wantedType, PointOfInterest exclude = null)
    {
        List<PointOfInterest> candidates = new List<PointOfInterest>();

        foreach (PointOfInterest poi in all)
        {
            if (poi.type == wantedType && poi != exclude)
            {
                candidates.Add(poi);
            }
        }

        // Si la única opción era la excluida, la usamos igualmente
        if (candidates.Count == 0 && exclude != null && exclude.type == wantedType)
        {
            return exclude;
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>Devuelve el punto de interés más cercano de ese tipo (o null si no hay).</summary>
    public static PointOfInterest GetNearest(PointOfInterestType wantedType, Vector3 from)
    {
        PointOfInterest best = null;
        float bestDistance = float.MaxValue;

        foreach (PointOfInterest poi in all)
        {
            if (poi.type != wantedType)
            {
                continue;
            }

            Vector3 delta = poi.transform.position - from;
            delta.y = 0f;
            float distance = delta.sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = poi;
            }
        }

        return best;
    }

    private void OnDrawGizmos()
    {
        switch (type)
        {
            case PointOfInterestType.Plaza:
                Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
                break;
            case PointOfInterestType.Shelter:
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
                break;
            default:
                Gizmos.color = new Color(1f, 0.4f, 0.8f, 0.9f);
                break;
        }

        float drawRadius = radius > 0f ? radius : 0.4f;
        Gizmos.DrawWireSphere(transform.position, drawRadius);
    }
}