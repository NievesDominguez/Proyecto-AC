using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un objeto del mundo que el jugador puede recoger.
/// No necesita collider: se detecta por distancia.
/// </summary>
public class WorldItem : MonoBehaviour
{
    private static readonly List<WorldItem> all = new List<WorldItem>();

    [SerializeField] private ItemData item;
    [SerializeField, Min(1)] private int amount = 1;

    [Tooltip("Pinta el modelo con el color provisional del objeto. Desactívalo si usas tu propio modelo.")]
    [SerializeField] private bool tintWithItemColor = true;

    [Header("Animación")]
    [SerializeField] private bool animate = true;
    [SerializeField] private float spinSpeed = 60f;
    [SerializeField] private float bobHeight = 0.08f;
    [SerializeField] private float bobSpeed = 2f;

    public ItemData Item => item;

    public int Amount
    {
        get => amount;
        set => amount = Mathf.Max(1, value);
    }

    public string PromptText { get; private set; }

    private float baseY;
    private float phase;

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

    private void Start()
    {
        baseY = transform.position.y;
        phase = Random.value * Mathf.PI * 2f;

        RefreshPrompt();
        ApplyTint();
    }

    private void Update()
    {
        if (!animate)
        {
            return;
        }

        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        Vector3 position = transform.position;
        position.y = baseY + Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
        transform.position = position;
    }

    public void Configure(ItemData newItem, int newAmount)
    {
        item = newItem;
        amount = Mathf.Max(1, newAmount);
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        PromptText = "[E] Coger";
    }

    private void ApplyTint()
    {
        if (!tintWithItemColor || item == null || item.worldPrefab != null)
        {
            return;
        }

        Renderer r = GetComponentInChildren<Renderer>();

        if (r != null)
        {
            r.material.color = item.placeholderColor;
        }
    }

    // ------------------------------------------------------------------
    // BÚSQUEDA
    // ------------------------------------------------------------------

    /// <summary>El objeto recogible más cercano dentro del radio (o null).</summary>
    public static WorldItem GetNearest(Vector3 from, float radius)
    {
        WorldItem best = null;
        float bestSqr = radius * radius;

        foreach (WorldItem candidate in all)
        {
            Vector3 delta = candidate.transform.position - from;

            // Ignoramos objetos a otra altura (otro piso, por ejemplo)
            if (Mathf.Abs(delta.y) > 2.5f)
            {
                continue;
            }

            delta.y = 0f;
            float sqr = delta.sqrMagnitude;

            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = candidate;
            }
        }

        return best;
    }

    // ------------------------------------------------------------------
    // CREACIÓN
    // ------------------------------------------------------------------

    /// <summary>
    /// Crea un objeto en el mundo: usa el prefab del ItemData si lo tiene
    /// y, si no, una esfera o un cubo provisional.
    /// </summary>
    public static WorldItem Spawn(ItemData data, int amount, Vector3 position)
    {
        GameObject go;
        bool usesPrefab = data.worldPrefab != null;

        if (usesPrefab)
        {
            go = Instantiate(data.worldPrefab, position, Quaternion.identity);
        }
        else
        {
            PrimitiveType shape = data.placeholderShape == PlaceholderShape.Sphere
                ? PrimitiveType.Sphere
                : PrimitiveType.Cube;

            go = GameObject.CreatePrimitive(shape);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.5f;

            // Sin collider: no estorba al jugador ni a los aldeanos
            Collider col = go.GetComponent<Collider>();

            if (col != null)
            {
                DestroyObject(col);
            }
        }

        go.name = $"Item_{data.displayName}";

        WorldItem worldItem = go.GetComponent<WorldItem>();

        if (worldItem == null)
        {
            worldItem = go.AddComponent<WorldItem>();
        }

        worldItem.tintWithItemColor = !usesPrefab;
        worldItem.Configure(data, amount);

        return worldItem;
    }

    // Permite usar el mismo código en el editor (para la escena de prueba) y en juego
    private static void DestroyObject(Object target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
