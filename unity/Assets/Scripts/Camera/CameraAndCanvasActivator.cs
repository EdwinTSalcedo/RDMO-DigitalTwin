using UnityEngine;
using System.Collections;
using System.IO;
using System.Diagnostics;

/// <summary>
/// Script que graba DOS videos separados (una cámara cada uno),
/// ambos con el Canvas superpuesto.
/// </summary>
public class CameraAndCanvasActivator : MonoBehaviour
{
    [Header("Cámara izquierda")]
    public Camera leftCamera;

    [Header("Cámara derecha (opcional)")]
    public Camera rightCamera;

    [Header("Canvas (opcional)")]
    [Tooltip("Canvas que se mostrará en AMBAS grabaciones.")]
    public Canvas targetCanvas;

    [Header("Estado")]
    [Tooltip("Activa la vista lado a lado con ambas cámaras y el Canvas.")]
    public bool active = false;

    [Header("Grabación")]
    [Tooltip("Activa/desactiva la grabación de video.")]
    public bool recording = false;

    [Header("Configuración de video")]
    public int resolutionWidth = 1280;
    public int resolutionHeight = 720;
    public int fps = 10;
    public string videoBitrate = "2M";
    public string outputFolder = "DigitalTwin_Logs/Recordings";
    public bool flipVerticalForFfmpeg = true;

    [Header("FFmpeg")]
    public string ffmpegPath = "ffmpeg";

    // ── Estado original de las cámaras ──
    private Rect leftOriginalRect;
    private bool leftOriginalEnabled;
    private Rect rightOriginalRect;
    private bool rightOriginalEnabled;

    // ── Estado original del Canvas ──
    private RenderMode originalCanvasRenderMode;
    private Camera originalCanvasCamera;
    private float originalCanvasPlaneDistance;

    // ── Grabación ──
    private bool isRecording = false;
    private Coroutine recordingCoroutine;
    private bool ffmpegAvailable = false;
    private Process ffmpegProcess1;
    private Process ffmpegProcess2;
    private string currentOutputFolder;
    private int frameCounter = 0;
    private byte[] flippedFrameBuffer;

    private void Start()
    {
        // Verificar FFmpeg
        try
        {
            using (Process test = new Process())
            {
                test.StartInfo.FileName = ffmpegPath;
                test.StartInfo.Arguments = "-version";
                test.StartInfo.UseShellExecute = false;
                test.StartInfo.RedirectStandardOutput = true;
                test.StartInfo.RedirectStandardError = true;
                test.StartInfo.CreateNoWindow = true;
                test.Start();
                test.WaitForExit(2000);
                ffmpegAvailable = test.ExitCode == 0;
            }
        }
        catch
        {
            ffmpegAvailable = false;
        }

        // Guardar estado original
        if (leftCamera != null)
        {
            leftOriginalRect = leftCamera.rect;
            leftOriginalEnabled = leftCamera.enabled;
        }
        if (rightCamera != null)
        {
            rightOriginalRect = rightCamera.rect;
            rightOriginalEnabled = rightCamera.enabled;
        }
        if (targetCanvas != null)
        {
            originalCanvasRenderMode = targetCanvas.renderMode;
            originalCanvasCamera = targetCanvas.worldCamera;
            originalCanvasPlaneDistance = targetCanvas.planeDistance;
        }

        ApplyState();
    }

    private void Update()
    {
        if (recording && !isRecording)
            StartManualRecording();
        else if (!recording && isRecording)
            StopManualRecording();
    }

    public void SetActive(bool value)
    {
        if (value == active) return;

        if (value)
        {
            if (leftCamera != null)
            {
                leftOriginalRect = leftCamera.rect;
                leftOriginalEnabled = leftCamera.enabled;
            }
            if (rightCamera != null)
            {
                rightOriginalRect = rightCamera.rect;
                rightOriginalEnabled = rightCamera.enabled;
            }
            if (targetCanvas != null)
            {
                originalCanvasRenderMode = targetCanvas.renderMode;
                originalCanvasCamera = targetCanvas.worldCamera;
                originalCanvasPlaneDistance = targetCanvas.planeDistance;
            }
        }

        active = value;
        ApplyState();
    }

    public void Toggle()
    {
        SetActive(!active);
    }

    private void StartManualRecording()
    {
        if (isRecording) return;

        string basePath = Path.Combine(Application.dataPath, "..", outputFolder);
        currentOutputFolder = Path.Combine(basePath, "Manual");
        Directory.CreateDirectory(currentOutputFolder);

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string videoName1 = $"Recording_{timestamp}_cam1.mp4";
        string videoName2 = rightCamera != null ? $"Recording_{timestamp}_cam2.mp4" : null;
        string videoPath1 = Path.Combine(currentOutputFolder, videoName1);
        string videoPath2 = videoName2 != null ? Path.Combine(currentOutputFolder, videoName2) : null;

        frameCounter = 0;
        isRecording = true;
        recordingCoroutine = StartCoroutine(RecordingLoop(videoPath1, videoPath2));
    }

    private void StopManualRecording()
    {
        if (!isRecording) return;

        isRecording = false;
        if (recordingCoroutine != null)
        {
            StopCoroutine(recordingCoroutine);
            recordingCoroutine = null;
        }

        CloseFFmpeg(ref ffmpegProcess1);
        CloseFFmpeg(ref ffmpegProcess2);

        UnityEngine.Debug.Log($"[CameraAndCanvas] ⏹ Grabación detenida. {frameCounter} frames en: {currentOutputFolder}");
    }

    private void CloseFFmpeg(ref Process process)
    {
        if (process != null && !process.HasExited)
        {
            try
            {
                process.StandardInput.Close();
                process.WaitForExit(3000);
                if (!process.HasExited) process.Kill();
            }
            catch { }
            process.Dispose();
            process = null;
        }
    }

    private IEnumerator RecordingLoop(string videoPath1, string videoPath2)
    {
        bool hasSecondary = rightCamera != null && videoPath2 != null;

        // RenderTextures
        RenderTexture rt1 = new RenderTexture(resolutionWidth, resolutionHeight, 24, RenderTextureFormat.ARGB32);
        rt1.antiAliasing = 1;
        Texture2D tex1 = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);

        RenderTexture rt2 = null;
        Texture2D tex2 = null;
        if (hasSecondary)
        {
            rt2 = new RenderTexture(resolutionWidth, resolutionHeight, 24, RenderTextureFormat.ARGB32);
            rt2.antiAliasing = 1;
            tex2 = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);
        }

        // Iniciar FFmpeg 1
        StartFFmpeg(ref ffmpegProcess1, videoPath1);

        // Iniciar FFmpeg 2
        if (hasSecondary)
            StartFFmpeg(ref ffmpegProcess2, videoPath2);

        float captureInterval = 1f / fps;

        while (isRecording)
        {
            // ── Cámara 1 (izquierda) con Canvas ──
            leftCamera.targetTexture = rt1;
            if (targetCanvas != null)
            {
                targetCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                targetCanvas.worldCamera = leftCamera;
                targetCanvas.planeDistance = 0.5f;
            }
            leftCamera.Render();
            RenderTexture.active = rt1;
            tex1.ReadPixels(new Rect(0, 0, resolutionWidth, resolutionHeight), 0, 0);
            tex1.Apply();
            SendFrame(tex1, ref ffmpegProcess1);

            // ── Cámara 2 (derecha) con Canvas ──
            if (hasSecondary)
            {
                rightCamera.targetTexture = rt2;
                if (targetCanvas != null)
                {
                    targetCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    targetCanvas.worldCamera = rightCamera;
                    targetCanvas.planeDistance = 0.5f;
                }
                rightCamera.Render();
                RenderTexture.active = rt2;
                tex2.ReadPixels(new Rect(0, 0, resolutionWidth, resolutionHeight), 0, 0);
                tex2.Apply();
                SendFrame(tex2, ref ffmpegProcess2);
            }

            frameCounter++;
            yield return new WaitForSeconds(captureInterval);
        }

        // Limpiar
        leftCamera.targetTexture = null;
        if (hasSecondary) rightCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt1);
        Destroy(tex1);
        if (rt2 != null) Destroy(rt2);
        if (tex2 != null) Destroy(tex2);

        CloseFFmpeg(ref ffmpegProcess1);
        CloseFFmpeg(ref ffmpegProcess2);
    }

    private void StartFFmpeg(ref Process process, string videoPath)
    {
        if (!ffmpegAvailable) return;

        try
        {
            process = new Process();
            process.StartInfo.FileName = ffmpegPath;
            process.StartInfo.Arguments = $"-y " +
                $"-f rawvideo -vcodec rawvideo -pix_fmt rgb24 " +
                $"-s {resolutionWidth}x{resolutionHeight} -r {fps} -i - " +
                $"-c:v libx264 -pix_fmt yuv420p -b:v {videoBitrate} -preset fast " +
                $"\"{videoPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[CameraAndCanvas] Error FFmpeg: {ex.Message}");
            ffmpegAvailable = false;
        }
    }

    private void SendFrame(Texture2D tex, ref Process process)
    {
        if (ffmpegAvailable && process != null && !process.HasExited)
        {
            byte[] rawData = tex.GetRawTextureData();
            byte[] frameData = flipVerticalForFfmpeg ? FlipFrameVertically(rawData, tex.width, tex.height) : rawData;
            try
            {
                process.StandardInput.BaseStream.Write(frameData, 0, frameData.Length);
                process.StandardInput.BaseStream.Flush();
            }
            catch { ffmpegAvailable = false; }
        }
        else
        {
            string filename = Path.Combine(currentOutputFolder, $"frame_{frameCounter:D6}.png");
            File.WriteAllBytes(filename, tex.EncodeToPNG());
        }
    }

    private byte[] FlipFrameVertically(byte[] source, int width, int height)
    {
        int rowSize = width * 3;
        int expectedSize = rowSize * height;
        if (source == null || source.Length != expectedSize) return source;

        if (flippedFrameBuffer == null || flippedFrameBuffer.Length != source.Length)
            flippedFrameBuffer = new byte[source.Length];

        for (int y = 0; y < height; y++)
            System.Buffer.BlockCopy(source, y * rowSize, flippedFrameBuffer, (height - 1 - y) * rowSize, rowSize);

        return flippedFrameBuffer;
    }

    private void ApplyState()
    {
        if (active)
        {
            bool hasRight = rightCamera != null;

            if (leftCamera != null)
            {
                leftCamera.rect = hasRight ? new Rect(0f, 0f, 0.5f, 1f) : new Rect(0f, 0f, 1f, 1f);
                leftCamera.enabled = true;
            }

            if (rightCamera != null)
            {
                rightCamera.rect = new Rect(0.5f, 0f, 0.5f, 1f);
                rightCamera.enabled = true;
            }

            if (targetCanvas != null)
            {
                targetCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                targetCanvas.worldCamera = leftCamera;
                targetCanvas.planeDistance = 0.5f;
            }
        }
        else
        {
            if (leftCamera != null)
            {
                leftCamera.rect = leftOriginalRect;
                leftCamera.enabled = leftOriginalEnabled;
            }
            if (rightCamera != null)
            {
                rightCamera.rect = rightOriginalRect;
                rightCamera.enabled = rightOriginalEnabled;
            }
            if (targetCanvas != null)
            {
                targetCanvas.renderMode = originalCanvasRenderMode;
                targetCanvas.worldCamera = originalCanvasCamera;
                targetCanvas.planeDistance = originalCanvasPlaneDistance;
            }
        }
    }

    private void OnDestroy()
    {
        if (isRecording)
        {
            isRecording = false;
            if (recordingCoroutine != null)
            {
                StopCoroutine(recordingCoroutine);
                recordingCoroutine = null;
            }
            CloseFFmpeg(ref ffmpegProcess1);
            CloseFFmpeg(ref ffmpegProcess2);
        }
    }
}