using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prepara de un clic la iluminación dinámica y los efectos del clima.
/// Este archivo debe estar dentro de una carpeta llamada "Editor".
/// Se puede ejecutar varias veces sin duplicar nada.
/// </summary>
public static class WorldSetupTool
{
    private const string MaterialFolder = "Assets/Materials";
    private const string MaterialPath = "Assets/Materials/WeatherParticle.mat";

    [MenuItem("Tools/World/Preparar iluminación y clima")]
    public static void Setup()
    {
        // --- WorldState ---
        WorldState world = Object.FindFirstObjectByType<WorldState>();

        if (world == null)
        {
            GameObject worldObject = new GameObject("WorldState");
            world = worldObject.AddComponent<WorldState>();
            Undo.RegisterCreatedObjectUndo(worldObject, "Crear WorldState");
        }

        // --- Sol (luz direccional) ---
        Light sun = FindSun();

        if (sun == null)
        {
            GameObject sunObject = new GameObject("Sun");
            sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            Undo.RegisterCreatedObjectUndo(sunObject, "Crear sol");
        }

        // La luz debe ser en tiempo real para poder moverse y cambiar durante la partida
        sun.lightmapBakeType = LightmapBakeType.Realtime;

        if (sun.shadows == LightShadows.None)
        {
            sun.shadows = LightShadows.Soft;
        }

        // --- Controlador de la atmósfera ---
        if (Object.FindFirstObjectByType<AtmosphereController>() == null)
        {
            GameObject atmosphereObject = new GameObject("Atmosphere");
            atmosphereObject.AddComponent<AtmosphereController>();
            Undo.RegisterCreatedObjectUndo(atmosphereObject, "Crear atmósfera");
        }

        // --- Efectos del clima ---
        if (Object.FindFirstObjectByType<WeatherEffects>() == null)
        {
            CreateWeatherEffects();
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "Iluminación y clima preparados. Para probar los días y las estaciones rápido, " +
            "selecciona 'WorldState' y pon Clock Mode en Accelerated."
        );
    }

    // ------------------------------------------------------------------
    // SOL
    // ------------------------------------------------------------------
    private static Light FindSun()
    {
        if (RenderSettings.sun != null)
        {
            return RenderSettings.sun;
        }

        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional)
            {
                return light;
            }
        }

        return null;
    }

    // ------------------------------------------------------------------
    // EFECTOS DEL CLIMA
    // ------------------------------------------------------------------
    private static void CreateWeatherEffects()
    {
        Material material = GetOrCreateParticleMaterial();

        GameObject root = new GameObject("WeatherFX");
        root.AddComponent<WeatherEffects>();
        Undo.RegisterCreatedObjectUndo(root, "Crear efectos del clima");

        CreateRain(root.transform, material);
        CreateSnow(root.transform, material);
    }

    private static void CreateRain(Transform parent, Material material)
    {
        GameObject go = new GameObject("Rain");
        go.transform.SetParent(parent, false);

        // Giramos el sistema para que su "adelante" apunte hacia abajo:
        // las gotas salen de un rectángulo horizontal situado por encima de la cámara
        go.transform.localPosition = new Vector3(0f, 12f, 0f);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 1.0f;
        main.startSpeed = 20f;
        main.startSize = 0.04f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.85f, 1f, 0.45f));
        main.maxParticles = 8000;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 30f, 0.1f);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1f;
        renderer.velocityScale = 0.04f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Undo.RegisterCreatedObjectUndo(go, "Crear lluvia");
    }

    private static void CreateSnow(Transform parent, Material material)
    {
        GameObject go = new GameObject("Snow");
        go.transform.SetParent(parent, false);

        go.transform.localPosition = new Vector3(0f, 12f, 0f);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 9f;
        main.startSpeed = 1.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f));
        main.maxParticles = 8000;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 30f, 0.1f);

        // Un ligero vaivén para que los copos floten en vez de caer en línea recta
        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.2f;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Undo.RegisterCreatedObjectUndo(go, "Crear nieve");
    }

    // Material sencillo con transparencia que usa el color de cada partícula
    private static Material GetOrCreateParticleMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (existing != null)
        {
            return existing;
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        if (shader == null)
        {
            Debug.LogWarning(
                "No se encontró un shader para las partículas del clima. " +
                "Asigna un material a mano en los sistemas Rain y Snow."
            );
            return null;
        }

        Material material = new Material(shader);
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();

        return material;
    }
}
