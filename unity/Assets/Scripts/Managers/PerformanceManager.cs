using UnityEngine;

public class PerformanceManager : MonoBehaviour
{
    private static PerformanceManager _instance;
    public static PerformanceManager Instance { get { return _instance; } }

    [Header("Performance Monitoring")]
    public float checkInterval = 1.0f;
    public float targetFPS = 30.0f;
    
    [Header("Dynamic Scaling")]
    public float currentScale = 1.0f; // 1.0 = Max Quality, 0.1 = Min
    public int frameSkip = 0; // 0 = Run every frame, 1 = Run every 2nd frame...
    
    private float updateTimer = 0f;
    private int frameCount = 0;
    private float fps = 60f;

    // Events to notify subscribers (like YOLO) to degrade/upgrade
    public delegate void OnPerformanceChanged(float scale, int framesToSkip);
    public static event OnPerformanceChanged PerformanceChanged;

    private void Awake()
    {
        if (_instance != null && _instance != this) Destroy(this.gameObject);
        else { _instance = this; DontDestroyOnLoad(this.gameObject); }
    }

    private void Start()
    {
        // Initial detection
        if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer)
        {
            // Start conservatively on mobile/web
            targetFPS = 30f;
            currentScale = 0.5f; 
            frameSkip = 2; // Run every 3rd frame
        }
        else
        {
            // PC
            targetFPS = 50f;
            currentScale = 1.0f;
            frameSkip = 0;
        }

        NotifyChange();
    }

    private void Update()
    {
        updateTimer -= Time.deltaTime;
        frameCount++;

        if (updateTimer <= 0f)
        {
            fps = frameCount / checkInterval;
            frameCount = 0;
            updateTimer = checkInterval;

            AdjustQuality();
        }
    }

    private void AdjustQuality()
    {
        bool changed = false;

        if (fps < targetFPS - 5f)
        {
            // Performance is bad -> Downgrade
            if (frameSkip < 10) 
            {
                frameSkip++;
                changed = true;
                Debug.Log($"[PerformanceManager] Low FPS ({fps:F1}). Increasing FrameSkip to {frameSkip}");
            }
            else if (currentScale > 0.3f)
            {
                currentScale -= 0.1f;
                changed = true;
                Debug.Log($"[PerformanceManager] Low FPS ({fps:F1}). Reducing Scale to {currentScale:F1}");
            }
        }
        else if (fps > targetFPS + 10f)
        {
            // Performance is good -> Upgrade (slowly)
            if (currentScale < 1.0f)
            {
                currentScale += 0.05f;
                changed = true;
            }
            else if (frameSkip > 0)
            {
                frameSkip--;
                changed = true;
            }
        }

        if (changed) NotifyChange();
    }

    private void NotifyChange()
    {
        PerformanceChanged?.Invoke(currentScale, frameSkip);
    }
}
