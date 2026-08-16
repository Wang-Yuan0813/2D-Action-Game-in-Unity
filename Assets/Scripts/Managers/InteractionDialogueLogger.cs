using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class InteractionDialogueLogger : MonoBehaviour
{
    public static InteractionDialogueLogger Instance { get; private set; }

    [Header("Map Detection")]
    [SerializeField] private ParallaxManager parallaxManager;
    [SerializeField] private string interactionMapId = "map-interaction";

    [Header("Logging")]
    [SerializeField] private bool enableLogging = true;
    [SerializeField] private string logFolderName = "logs";
    [SerializeField] private bool includeRawModelOutput = true;
    [SerializeField] private bool includeModelDiagnostics = true;

    private StreamWriter writer;
    private string currentLogPath;
    private bool isInInteractionMap;
    private int requestSequence;

    public string CurrentLogPath => currentLogPath;
    public bool HasActiveLog => writer != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveParallaxManager();
    }

    private void OnEnable()
    {
        ResolveParallaxManager();
        if (parallaxManager != null)
        {
            parallaxManager.ActiveMapChanged += HandleActiveMapChanged;
            if (parallaxManager.ActiveMap != null)
                HandleActiveMapChanged(parallaxManager.ActiveMap);
        }
    }

    private void Start()
    {
        if (parallaxManager != null)
            HandleActiveMapChanged(parallaxManager.ActiveMap);
    }

    private void OnDisable()
    {
        if (parallaxManager != null)
            parallaxManager.ActiveMapChanged -= HandleActiveMapChanged;

        CloseCurrentLog("Logger disabled or scene changed");
        isInInteractionMap = false;
    }

    private void OnDestroy()
    {
        CloseCurrentLog("Logger destroyed");
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        CloseCurrentLog("Application quit");
    }

    public string BeginRequest(string requestType, string playerInput)
    {
        if (writer == null)
            return string.Empty;

        string requestId = $"R{++requestSequence:0000}";
        WriteLine("============================================================");
        WriteLine($"[{DateTime.Now:O}] [{requestType}] [REQUEST] [{requestId}]");
        WriteLine(string.Empty);
        WriteLine("Player input:");
        WriteLine(playerInput ?? string.Empty);
        WriteLine(string.Empty);
        return requestId;
    }

    public void RecordSuccess(
        string requestId,
        string requestType,
        string modelOutput,
        string diagnostics,
        string rawModelOutput)
    {
        if (writer == null || string.IsNullOrEmpty(requestId))
            return;

        WriteLine($"[{DateTime.Now:O}] [{requestType}] [SUCCESS] [{requestId}]");
        WriteLine(string.Empty);
        WriteLine("Model output:");
        WriteLine(modelOutput ?? string.Empty);

        if (includeModelDiagnostics && !string.IsNullOrWhiteSpace(diagnostics))
        {
            WriteLine(string.Empty);
            WriteLine("Model diagnostics:");
            WriteLine(diagnostics.Trim());
        }

        if (includeRawModelOutput && !string.IsNullOrWhiteSpace(rawModelOutput))
        {
            WriteLine(string.Empty);
            WriteLine("Raw model output:");
            WriteLine(rawModelOutput.Trim());
        }

        WriteLine(string.Empty);
    }

    public void RecordFailure(
        string requestId,
        string requestType,
        string error)
    {
        if (writer == null || string.IsNullOrEmpty(requestId))
            return;

        WriteLine($"[{DateTime.Now:O}] [{requestType}] [FAILED] [{requestId}]");
        WriteLine(string.Empty);
        WriteLine("Error:");
        WriteLine(error ?? "Unknown error");
        WriteLine(string.Empty);
    }

    private void HandleActiveMapChanged(MapParallaxGroup map)
    {
        bool isInteraction = map != null &&
            string.Equals(map.MapId, interactionMapId, StringComparison.OrdinalIgnoreCase);

        if (isInteraction && !isInInteractionMap)
            OpenNewLog(map);
        else if (!isInteraction && isInInteractionMap)
            CloseCurrentLog($"Left {interactionMapId}");

        isInInteractionMap = isInteraction;
    }

    private void OpenNewLog(MapParallaxGroup map)
    {
        CloseCurrentLog("Starting a new interaction-map session");
        if (!enableLogging)
            return;

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 6);
        string fileName = $"interaction_{timestamp}_{uniqueSuffix}.txt";

        Exception primaryException = null;
        string gameRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (!string.IsNullOrEmpty(gameRoot) &&
            TryOpenWriter(Path.Combine(gameRoot, logFolderName), fileName, out primaryException))
        {
            WriteHeader(map);
            return;
        }

        if (TryOpenWriter(
            Path.Combine(Application.persistentDataPath, logFolderName),
            fileName,
            out Exception fallbackException))
        {
            Debug.LogWarning(
                $"Could not write the interaction log beside the game. " +
                $"Using persistent data instead: {currentLogPath}\n{primaryException?.Message}",
                this);
            WriteHeader(map);
            return;
        }

        Debug.LogWarning(
            "Could not create an interaction dialogue log. " +
            $"Primary error: {primaryException?.Message}; " +
            $"fallback error: {fallbackException?.Message}",
            this);
    }

    private bool TryOpenWriter(
        string directory,
        string fileName,
        out Exception exception)
    {
        exception = null;
        try
        {
            Directory.CreateDirectory(directory);
            currentLogPath = Path.Combine(directory, fileName);
            writer = new StreamWriter(
                currentLogPath,
                false,
                new UTF8Encoding(false))
            {
                AutoFlush = true
            };
            return true;
        }
        catch (Exception openException)
        {
            exception = openException;
            writer = null;
            currentLogPath = null;
            return false;
        }
    }

    private void WriteHeader(MapParallaxGroup map)
    {
        BlackCatTurtleSoupApiClient apiClient =
            FindObjectOfType<BlackCatTurtleSoupApiClient>();

        WriteLine("Interaction Map Dialogue Log");
        WriteLine($"Started: {DateTime.Now:O}");
        WriteLine($"Scene: {SceneManager.GetActiveScene().name}");
        WriteLine($"Map: {(map != null ? map.MapId : interactionMapId)}");
        WriteLine($"Puzzle: {(apiClient != null ? apiClient.PuzzleId : "Unknown")}");
        WriteLine($"Game version: {Application.version}");
        WriteLine($"Log path: {currentLogPath}");
        WriteLine(string.Empty);

        Debug.Log($"Interaction dialogue log created: {currentLogPath}", this);
    }

    private void WriteLine(string value)
    {
        if (writer == null)
            return;

        try
        {
            writer.WriteLine(value);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to write interaction dialogue log: {exception.Message}", this);
            CloseWriterSilently();
        }
    }

    private void CloseCurrentLog(string reason)
    {
        if (writer == null)
            return;

        WriteLine("============================================================");
        WriteLine($"Ended: {DateTime.Now:O}");
        WriteLine($"Reason: {reason}");
        CloseWriterSilently();
    }

    private void CloseWriterSilently()
    {
        if (writer != null)
        {
            try
            {
                writer.Flush();
                writer.Dispose();
            }
            catch (Exception)
            {
                // Logging must never interrupt gameplay.
            }
        }

        writer = null;
        currentLogPath = null;
        requestSequence = 0;
    }

    private void ResolveParallaxManager()
    {
        if (parallaxManager == null)
            parallaxManager = FindObjectOfType<ParallaxManager>();
    }
}
