using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sistema de decoración: coloca objetos del inventario en el mundo sobre una cuadrícula.
///
/// Cómo funciona:
///  - El objeto se coloca delante del jugador, alineado a la cuadrícula y mirando hacia él.
///  - Si justo delante no hay espacio, prueba las casillas de los lados (sin salir de delante).
///  - Solo se puede colocar si el suelo es llano y la hitbox del objeto no se cruza con nada.
///  - Mientras se elige el sitio se ve una vista previa verde (válido) o roja (sin espacio).
///
/// Controles durante la colocación:
///   E o clic izquierdo: colocar    R: girar 90 grados    Escape o Q: cancelar
/// </summary>
public class DecorationPlacer : MonoBehaviour
{
    public static DecorationPlacer Instance { get; private set; }

    /// <summary>Otros sistemas (recoger, hablar...) consultan esto para no interferir.</summary>
    public static bool IsPlacing { get; private set; }

    [Header("Referencias (se buscan solas si se dejan vacías)")]
    [SerializeField] private Inventory inventory;

    [Header("Cuadrícula")]
    [Tooltip("Lado de cada casilla, en metros.")]
    [SerializeField, Min(0.25f)] private float cellSize = 1f;

    [Tooltip("Desplazamiento de la cuadrícula respecto al origen del mundo (en X y Z).")]
    [SerializeField] private Vector2 gridOrigin = Vector2.zero;

    [Header("Dónde se coloca")]
    [Tooltip("Distancia mínima entre el jugador y el borde más cercano del objeto.")]
    [SerializeField, Min(0f)] private float frontGap = 0.5f;

    [Tooltip("Cuántas casillas hacia cada lado se prueban si delante no hay sitio.")]
    [SerializeField, Min(0)] private int sideSearchCells = 3;

    [Header("Comprobaciones")]
    [Tooltip("Capas con las que no puede cruzarse la hitbox del objeto. Por defecto, todas.")]
    [SerializeField] private LayerMask blockingLayers = ~0;

    [Tooltip("Capas que cuentan como suelo.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Tooltip("Diferencia máxima de altura del suelo bajo el objeto (para no colocarlo sobre un desnivel).")]
    [SerializeField, Min(0f)] private float maxGroundVariation = 0.12f;

    [Tooltip("Margen que se resta a la hitbox al comprobar, para que dos objetos puedan quedar pegados.")]
    [SerializeField, Min(0f)] private float skin = 0.04f;

    [Tooltip("Altura sobre los pies del jugador desde la que se busca el suelo.")]
    [SerializeField, Min(0.1f)] private float groundRayStartHeight = 1.2f;

    [Tooltip("Cuánto se baja desde esa altura buscando el suelo.")]
    [SerializeField, Min(0.5f)] private float groundRayLength = 3f;

    [Header("Comportamiento")]
    [Tooltip("Si se activa, tras colocar uno se sigue en modo colocación mientras queden unidades.")]
    [SerializeField] private bool continuePlacing = false;

    [Header("Vista previa")]
    [SerializeField] private Color validColor = new Color(0.3f, 1f, 0.5f, 0.45f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.45f);

    [Tooltip("Material translúcido opcional para la vista previa. Si se deja vacío se crea uno.")]
    [SerializeField] private Material ghostMaterialTemplate;

    [SerializeField, Range(1f, 60f)] private float previewSmoothing = 25f;

    // Objeto que se está colocando
    private ItemData currentItem;
    private int preferredSlot = -1;
    private Transform player;

    // Vista previa
    private GameObject preview;
    private Material ghostMaterial;
    private bool previewPositioned;

    // Orientación
    private Vector2Int facing;
    private bool hasFacing;
    private int manualTurns;

    // Resultado de la última comprobación
    private bool candidateValid;
    private Vector3 candidateBase;
    private int candidateTurns;
    private Vector2 candidateExtent;

    private float inputReadyTime;
    private readonly Vector3[] samplePoints = new Vector3[5];

    // ------------------------------------------------------------------
    // CICLO DE VIDA
    // ------------------------------------------------------------------
    private void Awake()
    {
        Instance = this;

        if (inventory == null)
        {
            inventory = FindFirstObjectByType<Inventory>();
        }
    }

    private void OnDisable()
    {
        EndPlacement();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!IsPlacing)
        {
            return;
        }

        if (currentItem == null || player == null)
        {
            EndPlacement();
            return;
        }

        HandleInput();

        if (!IsPlacing)
        {
            return;
        }

        ComputePlacement();
        UpdatePreview();
        UpdatePrompt();
    }

    // ------------------------------------------------------------------
    // API PÚBLICA
    // ------------------------------------------------------------------

    /// <summary>Empieza a colocar un objeto. 'slotIndex' es la casilla de la que sale (opcional).</summary>
    public bool BeginPlacement(ItemData item, int slotIndex = -1)
    {
        if (item == null || !item.placeable)
        {
            return false;
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }

        if (player == null)
        {
            Debug.LogWarning("DecorationPlacer: no se encontró ningún objeto con el tag 'Player'.", this);
            return false;
        }

        if (IsPlacing)
        {
            EndPlacement();
        }

        currentItem = item;
        preferredSlot = slotIndex;
        manualTurns = 0;
        hasFacing = false;
        previewPositioned = false;

        BuildPreview();

        IsPlacing = true;

        // Evita que el clic que pulsó el botón "Colocar" cuente también como confirmación
        inputReadyTime = Time.unscaledTime + 0.25f;

        return true;
    }

    /// <summary>Cancela la colocación sin gastar el objeto.</summary>
    public void Cancel()
    {
        EndPlacement();
    }

    // ------------------------------------------------------------------
    // ENTRADA
    // ------------------------------------------------------------------
    private void HandleInput()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (Time.unscaledTime < inputReadyTime)
        {
            return;
        }

        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame)
            {
                EndPlacement();
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                manualTurns = (manualTurns + 1) % 4;
            }
        }

        bool confirm =
            (keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
            (mouse != null && mouse.leftButton.wasPressedThisFrame);

        if (confirm)
        {
            TryPlace();
        }
    }

    // ------------------------------------------------------------------
    // CÁLCULO DE DÓNDE VA EL OBJETO
    // ------------------------------------------------------------------
    private void ComputePlacement()
    {
        Vector3 forward = player.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.01f)
        {
            forward = hasFacing ? new Vector3(facing.x, 0f, facing.y) : Vector3.forward;
        }

        facing = SnapFacing(forward.normalized);
        hasFacing = true;

        // El objeto mira hacia el jugador: su "delante" apunta en sentido contrario al del jugador
        float yaw = Mathf.Atan2(-facing.x, -facing.y) * Mathf.Rad2Deg;
        int baseTurns = Mathf.RoundToInt(yaw / 90f);
        candidateTurns = ((baseTurns + manualTurns) % 4 + 4) % 4;

        // Con un giro de 90 o 270 grados, ancho y fondo se intercambian en el mundo
        bool swapped = candidateTurns % 2 == 1;
        Vector2 size = new Vector2(currentItem.footprint.x, currentItem.footprint.y) * cellSize;
        candidateExtent = swapped ? new Vector2(size.y, size.x) : size;

        // Se prueba primero justo delante y después hacia los lados: 0, +1, -1, +2, -2...
        int candidateCount = 1 + 2 * sideSearchCells;

        for (int i = 0; i < candidateCount; i++)
        {
            int shift = i == 0 ? 0 : ((i + 1) / 2) * (i % 2 == 1 ? 1 : -1);
            Vector3 center = ComputeCenter(shift);

            if (Evaluate(center, candidateExtent, out float groundY))
            {
                candidateBase = new Vector3(center.x, groundY, center.z);
                candidateValid = true;
                return;
            }
        }

        // Sin sitio: enseñamos la posición de delante en rojo
        Vector3 fallback = ComputeCenter(0);
        EvaluateGroundOnly(fallback, candidateExtent, out float fallbackY);

        candidateBase = new Vector3(fallback.x, fallbackY, fallback.z);
        candidateValid = false;
    }

    // Centro (en X y Z) del objeto: alineado a la cuadrícula, delante del jugador y
    // desplazado 'shift' casillas hacia su derecha (o izquierda si es negativo).
    private Vector3 ComputeCenter(int shift)
    {
        Vector2 p = new Vector2(player.position.x - gridOrigin.x, player.position.z - gridOrigin.y);

        bool forwardIsX = facing.x != 0;

        float extentForward = forwardIsX ? candidateExtent.x : candidateExtent.y;
        float extentLateral = forwardIsX ? candidateExtent.y : candidateExtent.x;

        float playerForward = forwardIsX ? p.x : p.y;
        float playerLateral = forwardIsX ? p.y : p.x;
        int forwardSign = forwardIsX ? facing.x : facing.y;

        // Eje de avance: el borde cercano cae en la primera línea de la cuadrícula
        // que deja el hueco mínimo respecto al jugador (nunca más cerca de él)
        float centerForward;

        if (forwardSign > 0)
        {
            float edge = Mathf.Ceil((playerForward + frontGap) / cellSize) * cellSize;
            centerForward = edge + extentForward / 2f;
        }
        else
        {
            float edge = Mathf.Floor((playerForward - frontGap) / cellSize) * cellSize;
            centerForward = edge - extentForward / 2f;
        }

        // Eje lateral: alineado a la cuadrícula, cerca del jugador, y desplazado hacia un lado.
        // La derecha del jugador es (facing.y, -facing.x) en el plano XZ.
        int rightSign = forwardIsX ? -facing.x : facing.y;

        float minLateral = Mathf.Round((playerLateral - extentLateral / 2f) / cellSize) * cellSize
                           + shift * cellSize * rightSign;
        float centerLateral = minLateral + extentLateral / 2f;

        float gridX = forwardIsX ? centerForward : centerLateral;
        float gridZ = forwardIsX ? centerLateral : centerForward;

        return new Vector3(gridX + gridOrigin.x, 0f, gridZ + gridOrigin.y);
    }

    // Convierte la dirección del jugador en una de las cuatro cardinales.
    // Con histéresis: no cambia de dirección hasta que se aleja claramente de la actual.
    private Vector2Int SnapFacing(Vector3 forward)
    {
        Vector2Int best = Mathf.Abs(forward.x) > Mathf.Abs(forward.z)
            ? new Vector2Int(forward.x > 0f ? 1 : -1, 0)
            : new Vector2Int(0, forward.z > 0f ? 1 : -1);

        if (hasFacing && best != facing)
        {
            Vector3 current = new Vector3(facing.x, 0f, facing.y);

            if (Vector3.Dot(forward, current) > 0.6f)
            {
                return facing;
            }
        }

        return best;
    }

    // ------------------------------------------------------------------
    // COMPROBACIONES
    // ------------------------------------------------------------------

    // Devuelve true si el objeto cabe: suelo llano debajo y la hitbox no se cruza con nada
    private bool Evaluate(Vector3 center, Vector2 extent, out float groundY)
    {
        if (!SampleGround(center, extent, out groundY))
        {
            return false;
        }

        float height = currentItem.placedHeight;

        Vector3 boxCenter = new Vector3(center.x, groundY + height / 2f, center.z);
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.01f, extent.x / 2f - skin),
            Mathf.Max(0.01f, height / 2f - skin),
            Mathf.Max(0.01f, extent.y / 2f - skin));

        // La caja está alineada con los ejes porque solo se usan giros de 90 grados.
        // No se ignoran los triggers de forma implícita: se ignoran a propósito.
        return !Physics.CheckBox(
            boxCenter,
            halfExtents,
            Quaternion.identity,
            blockingLayers,
            QueryTriggerInteraction.Ignore);
    }

    // Para dibujar la vista previa en rojo cuando no hay sitio
    private void EvaluateGroundOnly(Vector3 center, Vector2 extent, out float groundY)
    {
        if (!SampleGround(center, extent, out groundY))
        {
            groundY = player.position.y;
        }
    }

    // Busca el suelo bajo el centro y las cuatro esquinas. Falla si falta suelo en algún
    // punto, si el desnivel es grande o si debajo hay otro objeto colocado.
    private bool SampleGround(Vector3 center, Vector2 extent, out float groundY)
    {
        // Las esquinas se meten un poco hacia dentro para no tocar a los vecinos
        const float inset = 0.1f;
        float halfX = Mathf.Max(0f, extent.x / 2f - inset);
        float halfZ = Mathf.Max(0f, extent.y / 2f - inset);

        samplePoints[0] = new Vector3(center.x, 0f, center.z);
        samplePoints[1] = new Vector3(center.x - halfX, 0f, center.z - halfZ);
        samplePoints[2] = new Vector3(center.x + halfX, 0f, center.z - halfZ);
        samplePoints[3] = new Vector3(center.x - halfX, 0f, center.z + halfZ);
        samplePoints[4] = new Vector3(center.x + halfX, 0f, center.z + halfZ);

        float minY = float.MaxValue;
        float maxY = float.MinValue;
        float startY = player.position.y + groundRayStartHeight;

        groundY = player.position.y;

        foreach (Vector3 point in samplePoints)
        {
            Vector3 origin = new Vector3(point.x, startY, point.z);

            if (!Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    groundRayLength,
                    groundLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // No se pueden apilar objetos unos sobre otros
            if (hit.collider.GetComponentInParent<PlacedObject>() != null)
            {
                groundY = hit.point.y;
                return false;
            }

            minY = Mathf.Min(minY, hit.point.y);
            maxY = Mathf.Max(maxY, hit.point.y);
        }

        groundY = maxY;
        return maxY - minY <= maxGroundVariation;
    }

    // ------------------------------------------------------------------
    // COLOCAR
    // ------------------------------------------------------------------
    private void TryPlace()
    {
        if (!candidateValid)
        {
            return;
        }

        ItemData item = currentItem;

        if (inventory == null || !inventory.RemoveItem(item, 1, preferredSlot))
        {
            // Ya no queda ninguna unidad
            EndPlacement();
            return;
        }

        CreatePlacedObject(item, candidateBase, candidateTurns, candidateExtent);

        bool keepGoing = continuePlacing && inventory.CountOf(item) > 0;

        if (!keepGoing)
        {
            EndPlacement();
        }
        else
        {
            // Si el objeto vino de una casilla concreta y se vació, buscamos en cualquiera
            preferredSlot = -1;
        }
    }

    private void CreatePlacedObject(ItemData item, Vector3 basePosition, int quarterTurns, Vector2 extent)
    {
        Quaternion rotation = Quaternion.Euler(0f, quarterTurns * 90f, 0f);
        float height = item.placedHeight;
        float width = item.footprint.x * cellSize;
        float depth = item.footprint.y * cellSize;

        GameObject placed;

        if (item.placedPrefab != null)
        {
            placed = Instantiate(item.placedPrefab, basePosition, rotation);
        }
        else
        {
            placed = CreatePlaceholderObject(item, width, depth, height, basePosition, rotation);
        }

        placed.name = $"Placed_{item.displayName}";

        // Todo objeto colocado debe tener hitbox: si el prefab no trae ninguna, añadimos
        // una caja del tamaño de su huella
        if (placed.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider box = placed.AddComponent<BoxCollider>();
            box.size = new Vector3(width, height, depth);
            box.center = new Vector3(0f, height / 2f, 0f);
        }

        PlacedObject marker = placed.GetComponent<PlacedObject>();

        if (marker == null)
        {
            marker = placed.AddComponent<PlacedObject>();
        }

        Vector2Int occupied = new Vector2Int(
            Mathf.RoundToInt(extent.x / cellSize),
            Mathf.RoundToInt(extent.y / cellSize));

        marker.Initialize(item, quarterTurns, occupied);
    }

    // Bloque provisional para probar: ocupa su huella y tiene una marca en la parte delantera
    private static GameObject CreatePlaceholderObject(
        ItemData item,
        float width,
        float depth,
        float height,
        Vector3 position,
        Quaternion rotation)
    {
        const float margin = 0.06f;

        GameObject root = new GameObject("Placed");
        root.transform.SetPositionAndRotation(position, rotation);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(width - margin, height, depth - margin);
        body.transform.localPosition = new Vector3(0f, height / 2f, 0f);
        body.GetComponent<Renderer>().material.color = item.placeholderColor;

        GameObject front = GameObject.CreatePrimitive(PrimitiveType.Cube);
        front.name = "Front";
        Destroy(front.GetComponent<Collider>());
        front.transform.SetParent(root.transform, false);
        front.transform.localScale = new Vector3(Mathf.Min(0.4f, width * 0.4f), 0.15f, 0.1f);
        front.transform.localPosition = new Vector3(0f, height * 0.8f, depth / 2f - margin / 2f);

        Color c = item.placeholderColor;
        front.GetComponent<Renderer>().material.color = new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 1f);

        return root;
    }

    // ------------------------------------------------------------------
    // VISTA PREVIA
    // ------------------------------------------------------------------
    private void BuildPreview()
    {
        DestroyPreview();

        ghostMaterial = CreateGhostMaterial();

        preview = new GameObject("PlacementPreview");

        float width = currentItem.footprint.x * cellSize;
        float depth = currentItem.footprint.y * cellSize;
        float height = currentItem.placedHeight;

        // El objeto en sí
        if (currentItem.placedPrefab != null)
        {
            GameObject visual = Instantiate(currentItem.placedPrefab, preview.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            MakeGhost(visual);
        }
        else
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.SetParent(preview.transform, false);
            block.transform.localScale = new Vector3(width, height, depth);
            block.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            MakeGhost(block);
        }

        // Una losa plana en el suelo que enseña las casillas que ocupa
        GameObject footprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
        footprint.transform.SetParent(preview.transform, false);
        footprint.transform.localScale = new Vector3(width, 0.03f, depth);
        footprint.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        MakeGhost(footprint);
    }

    // Quita colisiones y scripts, y pone el material translúcido
    private void MakeGhost(GameObject root)
    {
        foreach (Collider col in root.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
            Destroy(col);
        }

        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>())
        {
            Destroy(body);
        }

        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>())
        {
            behaviour.enabled = false;
        }

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            Material[] materials = new Material[r.sharedMaterials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = ghostMaterial;
            }

            r.sharedMaterials = materials;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    private Material CreateGhostMaterial()
    {
        Material material;

        if (ghostMaterialTemplate != null)
        {
            material = new Material(ghostMaterialTemplate);
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            material = new Material(shader);
        }

        material.color = validColor;
        return material;
    }

    private void UpdatePreview()
    {
        if (preview == null)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.Euler(0f, candidateTurns * 90f, 0f);

        if (!previewPositioned)
        {
            preview.transform.SetPositionAndRotation(candidateBase, targetRotation);
            previewPositioned = true;
        }
        else
        {
            // Se desliza de casilla en casilla en vez de saltar
            float blend = 1f - Mathf.Exp(-previewSmoothing * Time.unscaledDeltaTime);

            preview.transform.position = Vector3.Lerp(preview.transform.position, candidateBase, blend);
            preview.transform.rotation = Quaternion.Slerp(preview.transform.rotation, targetRotation, blend);
        }

        ghostMaterial.color = candidateValid ? validColor : invalidColor;
    }

    private void DestroyPreview()
    {
        if (preview != null)
        {
            Destroy(preview);
            preview = null;
        }

        if (ghostMaterial != null)
        {
            Destroy(ghostMaterial);
            ghostMaterial = null;
        }
    }

    // ------------------------------------------------------------------
    // INDICACIONES EN PANTALLA
    // ------------------------------------------------------------------
    private void UpdatePrompt()
    {
        if (InteractionPrompt.Instance == null)
        {
            return;
        }

        string text = candidateValid
            ? $"[E] Colocar {currentItem.displayName}   ·   [R] Girar   ·   [Esc] Cancelar"
            : "Sin espacio aquí   ·   [R] Girar   ·   [Esc] Cancelar";

        InteractionPrompt.Instance.Set(this, true, text);
    }

    private void EndPlacement()
    {
        if (InteractionPrompt.Instance != null)
        {
            InteractionPrompt.Instance.Set(this, false);
        }

        DestroyPreview();

        currentItem = null;
        preferredSlot = -1;
        IsPlacing = false;
    }
}
