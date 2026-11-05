using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramientas para probar rápido el sistema de aldeanos.
/// Este archivo debe estar dentro de una carpeta llamada "Editor".
/// </summary>
public static class VillagerTestSetup
{
    private const string DataFolder = "Assets/Data/Villagers";

    // ------------------------------------------------------------------
    // 1. DATOS DE EJEMPLO
    // ------------------------------------------------------------------
    [MenuItem("Tools/Villagers/1 - Crear datos de ejemplo")]
    public static void CreateSampleDataMenu()
    {
        CreateSampleData();
        Debug.Log($"Datos de ejemplo creados en {DataFolder}");
    }

    public static VillagerData[] CreateSampleData()
    {
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Villagers");

        // Efusivo: social, enérgico, optimista, emocional, confiado
        VillagerData nuru = GetOrCreate("Villager_Nuru", d =>
        {
            d.displayName = "Nuru";
            d.species = "Acuático";
            d.description = "Sociable y enérgico. Siempre tiene algo que contar.";
            d.social = SocialTrait.Extrovertido;
            d.energy = EnergyTrait.Energico;
            d.outlook = OutlookTrait.Optimista;
            d.decision = DecisionTrait.Emocional;
            d.trust = TrustTrait.Confiado;
            d.peculiarity = Peculiarity.Competitivo;
            d.hobbies.Add(Hobby.Musica);
            d.hobbies.Add(Hobby.Exploracion);
            d.wakeHour = 7;
            d.bedtimeHour = 23;
        });

        // Receloso y noctámbulo: introvertido, tranquilo, pesimista, lógico, desconfiado
        VillagerData bram = GetOrCreate("Villager_Bram", d =>
        {
            d.displayName = "Bram";
            d.species = "Cristalino";
            d.description = "Callado y observador. Prefiere la noche.";
            d.social = SocialTrait.Introvertido;
            d.energy = EnergyTrait.Tranquilo;
            d.outlook = OutlookTrait.Pesimista;
            d.decision = DecisionTrait.Logico;
            d.trust = TrustTrait.Desconfiado;
            d.peculiarity = Peculiarity.Noctambulo;
            d.hobbies.Add(Hobby.Coleccionismo);
            d.wakeHour = 14;
            d.bedtimeHour = 5;
        });

        // Inquieto y soñador: introvertido, enérgico, optimista, emocional, desconfiado
        VillagerData sefa = GetOrCreate("Villager_Sefa", d =>
        {
            d.displayName = "Sefa";
            d.species = "Criatura del frío";
            d.description = "Mucha energía, pero la gasta casi siempre a solas.";
            d.social = SocialTrait.Introvertido;
            d.energy = EnergyTrait.Energico;
            d.outlook = OutlookTrait.Optimista;
            d.decision = DecisionTrait.Emocional;
            d.trust = TrustTrait.Desconfiado;
            d.peculiarity = Peculiarity.Sonador;
            d.hobbies.Add(Hobby.Jardineria);
            d.hobbies.Add(Hobby.Decoracion);
            d.wakeHour = 7;
            d.bedtimeHour = 23;
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return new[] { nuru, bram, sefa };
    }

    // ------------------------------------------------------------------
    // 2. ESCENA DE PRUEBA COMPLETA
    // ------------------------------------------------------------------
    [MenuItem("Tools/Villagers/2 - Crear escena de prueba")]
    public static void CreateTestScene()
    {
        VillagerData[] data = CreateSampleData();

        // Suelo (50 x 50)
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(5f, 1f, 5f);
        Undo.RegisterCreatedObjectUndo(ground, "Crear suelo");

        // Estado del mundo
        if (Object.FindFirstObjectByType<WorldState>() == null)
        {
            GameObject world = new GameObject("WorldState");
            world.AddComponent<WorldState>();
            Undo.RegisterCreatedObjectUndo(world, "Crear WorldState");
        }

        // Plaza
        CreatePointOfInterest(
            "Plaza",
            PointOfInterestType.Plaza,
            new Vector3(10f, 0.03f, 10f),
            new Vector3(10f, 0.05f, 10f),
            PrimitiveType.Cylinder,
            5f
        );

        // Flores
        Vector3[] flowerPositions =
        {
            new Vector3(-12f, 0.2f, 8f),
            new Vector3(-8f, 0.2f, -12f),
            new Vector3(14f, 0.2f, -6f),
            new Vector3(-15f, 0.2f, -3f),
            new Vector3(3f, 0.2f, 15f)
        };

        for (int i = 0; i < flowerPositions.Length; i++)
        {
            CreatePointOfInterest(
                $"Flower_{i + 1}",
                PointOfInterestType.Flower,
                flowerPositions[i],
                new Vector3(0.4f, 0.4f, 0.4f),
                PrimitiveType.Sphere,
                0f
            );
        }

        // Aldeanos
        Vector3[] spawns =
        {
            new Vector3(-2f, 1f, 0f),
            new Vector3(0f, 1f, 2f),
            new Vector3(2f, 1f, 0f)
        };

        for (int i = 0; i < data.Length; i++)
        {
            CreateVillager(data[i], spawns[i]);
        }

        // Casas y refugios
        AddHomesAndShelters();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = ground;

        Debug.Log(
            "Escena de prueba creada. IMPORTANTE: ahora hay que generar el NavMesh " +
            "(selecciona 'Ground' y haz Bake con un componente NavMeshSurface)."
        );
    }

    // ------------------------------------------------------------------
    // 3. CASAS Y REFUGIOS (para escenas que ya tienes)
    // ------------------------------------------------------------------
    [MenuItem("Tools/Villagers/3 - Añadir casas y refugios a la escena")]
    public static void AddHomesAndSheltersMenu()
    {
        AddHomesAndShelters();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "Casas y refugios añadidos. Vuelve a hacer Bake del NavMesh para que " +
            "los aldeanos esquiven las casas."
        );
    }

    /// <summary>
    /// Crea una casa para cada aldeano que aún no tenga una y dos refugios
    /// si la escena no tiene ninguno. Se puede ejecutar varias veces sin duplicar.
    /// </summary>
    public static void AddHomesAndShelters()
    {
        VillagerRoutine[] villagers =
            Object.FindObjectsByType<VillagerRoutine>(FindObjectsSortMode.None);

        for (int i = 0; i < villagers.Length; i++)
        {
            VillagerRoutine villager = villagers[i];

            if (villager.HasHome)
            {
                continue;
            }

            // Las casas se reparten en un círculo de 20 m alrededor del centro
            float angle = (i / (float)Mathf.Max(1, villagers.Length)) * Mathf.PI * 2f + 0.8f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            string owner = villager.GetComponent<VillagerPersonality>().DisplayName;

            GameObject house = GameObject.CreatePrimitive(PrimitiveType.Cube);
            house.name = $"House_{owner}";
            house.transform.position = direction * 20f + Vector3.up;
            house.transform.localScale = new Vector3(3f, 2f, 3f);
            Undo.RegisterCreatedObjectUndo(house, "Crear casa");

            // La puerta es un punto en el suelo, delante de la casa (hacia el centro)
            GameObject door = new GameObject($"Door_{owner}");
            door.transform.position = direction * 17f;
            Undo.RegisterCreatedObjectUndo(door, "Crear puerta");

            Undo.RecordObject(villager, "Asignar casa");
            villager.SetHome(door.transform);
            EditorUtility.SetDirty(villager);
        }

        bool hasShelter = false;

        foreach (PointOfInterest poi in Object.FindObjectsByType<PointOfInterest>(FindObjectsSortMode.None))
        {
            if (poi.Type == PointOfInterestType.Shelter)
            {
                hasShelter = true;
                break;
            }
        }

        if (!hasShelter)
        {
            CreateShelter("Shelter_1", new Vector3(-6f, 0f, -6f));
            CreateShelter("Shelter_2", new Vector3(6f, 0f, -9f));
        }
    }

    // ------------------------------------------------------------------
    // AUXILIARES
    // ------------------------------------------------------------------
    private static void CreateVillager(VillagerData data, Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = $"Villager_{data.displayName}";
        go.transform.position = position;

        // La cápsula tiene el pivote en el centro, así que subimos el agente 1 m
        NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
        agent.baseOffset = 1f;

        VillagerPersonality personality = go.AddComponent<VillagerPersonality>();
        personality.data = data;
        personality.ApplyData();

        go.AddComponent<VillagerRoutine>();
        go.AddComponent<VillagerDebugLabel>();

        Undo.RegisterCreatedObjectUndo(go, "Crear aldeano");
    }

    private static void CreatePointOfInterest(
        string objectName,
        PointOfInterestType type,
        Vector3 position,
        Vector3 scale,
        PrimitiveType shape,
        float radius)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = objectName;
        go.transform.position = position;
        go.transform.localScale = scale;

        // Sin colisión, para no estorbar a los aldeanos ni al NavMesh
        Collider col = go.GetComponent<Collider>();

        if (col != null)
        {
            Object.DestroyImmediate(col);
        }

        PointOfInterest poi = go.AddComponent<PointOfInterest>();
        poi.Configure(type, radius);

        Undo.RegisterCreatedObjectUndo(go, "Crear punto de interés");
    }

    // Un refugio es un punto en el suelo con un "techo" visual por encima
    private static void CreateShelter(string objectName, Vector3 position)
    {
        GameObject root = new GameObject(objectName);
        root.transform.position = position;

        PointOfInterest poi = root.AddComponent<PointOfInterest>();
        poi.Configure(PointOfInterestType.Shelter, 2f);

        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Roof";
        roof.transform.SetParent(root.transform, false);
        roof.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        roof.transform.localScale = new Vector3(4f, 0.2f, 4f);

        Collider col = roof.GetComponent<Collider>();

        if (col != null)
        {
            Object.DestroyImmediate(col);
        }

        Undo.RegisterCreatedObjectUndo(root, "Crear refugio");
    }

    private static VillagerData GetOrCreate(string fileName, System.Action<VillagerData> setup)
    {
        string path = $"{DataFolder}/{fileName}.asset";
        VillagerData existing = AssetDatabase.LoadAssetAtPath<VillagerData>(path);

        if (existing != null)
        {
            return existing;
        }

        VillagerData data = ScriptableObject.CreateInstance<VillagerData>();
        setup(data);
        AssetDatabase.CreateAsset(data, path);

        return data;
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{folderName}"))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}