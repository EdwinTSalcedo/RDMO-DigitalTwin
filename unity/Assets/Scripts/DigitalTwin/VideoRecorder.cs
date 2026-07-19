using UnityEngine;
using System.Collections;
using System.IO;
using System.Diagnostics;

namespace DigitalTwin
{
    /// <summary>
    /// Graba video directamente a MP4 usando FFmpeg por pipe (sin guardar PNGs individuales).
    /// Envía frames en crudo (bmp) por stdin de FFmpeg, que codifica a H.264 sobre la marcha.
    /// 
    /// Soporta grabación simultánea de DOS cámaras en archivos separados.
    /// </summary>
    public class VideoRecorder : MonoBehaviour
    {
        [Header("Cámara principal")]
        [Tooltip("Si está vacío, busca automáticamente: hijas → propio → Camera.main")]
        public Camera targetCamera;

        [Header("Cámara secundaria (opcional)")]
        [Tooltip("Si se asigna, grabará ambas cámaras simultáneamente en archivos separados.")]
        public Camera secondaryCamera;

        [Header("Configuración de grabación")]
        public int resolutionWidth = 1280;
        public int resolutionHeight = 720;
        public int fps = 10;
        [Tooltip("Bitrate del video (mayor = mejor calidad pero más peso). 2M = bueno.")]
        public string videoBitrate = "2M";
        public string outputFolder = "DigitalTwin_Logs/Recordings";
        public bool flipVerticalForFfmpeg = true;

        [Header("FFmpeg")]
        [Tooltip("Ruta al ejecutable ffmpeg. Si está en PATH, dejar 'ffmpeg'.")]
        public string ffmpegPath = "ffmpeg";

        public bool isRecording = false;
        private Coroutine recordingCoroutine;
        private string currentEpisodeFolder;
        private int frameCounter = 0;
        private bool ffmpegAvailable = false;

        // ── Cámara 1 ──
        private Camera activeCamera;
        private RenderTexture camRenderTexture;
        private Texture2D camTexture;
        private Process ffmpegProcess;

        // ── Cámara 2 ──
        private Camera activeSecondaryCamera;
        private RenderTexture cam2RenderTexture;
        private Texture2D cam2Texture;
        private Process ffmpegProcess2;

        private byte[] flippedFrameBuffer;

        private void Start()
        {
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

            if (!ffmpegAvailable)
                UnityEngine.Debug.LogWarning("[VideoRecorder] ⚠ FFmpeg no encontrado. Los frames se guardarán como PNG.");
            else
                UnityEngine.Debug.Log($"[VideoRecorder] ✅ FFmpeg disponible en: {ffmpegPath}");
        }

        public void StartRecording(int episodeNumber)
        {
            if (isRecording)
            {
                UnityEngine.Debug.LogWarning("[VideoRecorder] Ya se está grabando. Deteniendo grabación anterior...");
                StopRecording();
            }

            Camera cameraToUse = targetCamera;
            if (cameraToUse == null)
                cameraToUse = GetComponentInChildren<Camera>();
            if (cameraToUse == null)
                cameraToUse = GetComponent<Camera>();
            if (cameraToUse == null)
                cameraToUse = Camera.main;

            if (cameraToUse == null)
            {
                UnityEngine.Debug.LogError("[VideoRecorder] No hay cámara disponible para grabar.");
                return;
            }

            Camera secondaryToUse = secondaryCamera;

            if (secondaryToUse != null)
                UnityEngine.Debug.Log($"[VideoRecorder] 📹 Grabando 2 cámaras: {cameraToUse.name} + {secondaryToUse.name}");
            else
                UnityEngine.Debug.Log($"[VideoRecorder] 📹 Grabando 1 cámara: {cameraToUse.name}");

            string basePath = Path.Combine(Application.dataPath, "..", outputFolder);
            currentEpisodeFolder = Path.Combine(basePath, $"Ep_{episodeNumber}");
            Directory.CreateDirectory(currentEpisodeFolder);

            frameCounter = 0;
            isRecording = true;
            recordingCoroutine = StartCoroutine(RecordingLoop(cameraToUse, secondaryToUse, episodeNumber));

            UnityEngine.Debug.Log($"[VideoRecorder] 🎬 Iniciando grabación Episodio #{episodeNumber} en: {currentEpisodeFolder}");
        }

        public void StopRecording()
        {
            if (!isRecording) return;

            isRecording = false;
            if (recordingCoroutine != null)
            {
                StopCoroutine(recordingCoroutine);
                recordingCoroutine = null;
                CleanupRecordingResources();
            }

            CloseFFmpegProcess(ref ffmpegProcess);
            CloseFFmpegProcess(ref ffmpegProcess2);

            if (ffmpegAvailable)
                UnityEngine.Debug.Log($"[VideoRecorder] ⏹ Grabación detenida. {frameCounter} frames → video MP4 en: {currentEpisodeFolder}");
            else
                UnityEngine.Debug.Log($"[VideoRecorder] ⏹ Grabación detenida. {frameCounter} frames PNG guardados en: {currentEpisodeFolder}");
        }

        private void CloseFFmpegProcess(ref Process process)
        {
            if (process != null && !process.HasExited)
            {
                try
                {
                    process.StandardInput.Close();
                    process.WaitForExit(3000);
                    if (!process.HasExited)
                        process.Kill();
                }
                catch { }
                process.Dispose();
                process = null;
            }
        }

        private IEnumerator RecordingLoop(Camera cameraToUse, Camera secondaryToUse, int episodeNumber)
        {
            bool hasSecondary = secondaryToUse != null;

            // ── Configurar RenderTextures (sin .Create()) ──
            camRenderTexture = new RenderTexture(resolutionWidth, resolutionHeight, 24, RenderTextureFormat.ARGB32);
            camRenderTexture.antiAliasing = 1;
            camTexture = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);
            activeCamera = cameraToUse;

            string videoPath = Path.Combine(currentEpisodeFolder, $"Ep_{episodeNumber}.mp4");

            if (hasSecondary)
            {
                cam2RenderTexture = new RenderTexture(resolutionWidth, resolutionHeight, 24, RenderTextureFormat.ARGB32);
                cam2RenderTexture.antiAliasing = 1;
                cam2Texture = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);
                activeSecondaryCamera = secondaryToUse;
            }

            string videoPath2 = hasSecondary ? Path.Combine(currentEpisodeFolder, $"Ep_{episodeNumber}_cam2.mp4") : null;

            // ── Iniciar FFmpeg ──
            StartFFmpegProcess(ref ffmpegProcess, videoPath);
            if (hasSecondary)
                StartFFmpegProcess(ref ffmpegProcess2, videoPath2);

            float captureInterval = 1f / fps;

            while (isRecording)
            {
                // ── Cámara 1 ──
                cameraToUse.targetTexture = camRenderTexture;
                cameraToUse.Render();
                RenderTexture.active = camRenderTexture;
                camTexture.ReadPixels(new Rect(0, 0, resolutionWidth, resolutionHeight), 0, 0);
                camTexture.Apply();
                SendFrameToFFmpeg(camTexture, ref ffmpegProcess, 1);
                RenderTexture.active = null;

                // ── Cámara 2 ──
                if (hasSecondary)
                {
                    yield return null; // Esperar a que la GPU termine
                    secondaryToUse.targetTexture = cam2RenderTexture;
                    secondaryToUse.Render();
                    RenderTexture.active = cam2RenderTexture;
                    cam2Texture.ReadPixels(new Rect(0, 0, resolutionWidth, resolutionHeight), 0, 0);
                    cam2Texture.Apply();
                    SendFrameToFFmpeg(cam2Texture, ref ffmpegProcess2, 2);
                    RenderTexture.active = null;
                }

                frameCounter++;
                yield return new WaitForSeconds(captureInterval);
            }

            cameraToUse.targetTexture = null;
            if (hasSecondary) secondaryToUse.targetTexture = null;

            CloseFFmpegProcess(ref ffmpegProcess);
            if (hasSecondary)
                CloseFFmpegProcess(ref ffmpegProcess2);
        }

        private void StartFFmpegProcess(ref Process process, string videoPath)
        {
            if (!ffmpegAvailable) return;

            try
            {
                process = new Process();
                process.StartInfo.FileName = ffmpegPath;
                process.StartInfo.Arguments = $"-y " +
                    $"-f rawvideo " +
                    $"-vcodec rawvideo " +
                    $"-pix_fmt rgb24 " +
                    $"-s {resolutionWidth}x{resolutionHeight} " +
                    $"-r {fps} " +
                    $"-i - " +
                    $"-c:v libx264 " +
                    $"-pix_fmt yuv420p " +
                    $"-b:v {videoBitrate} " +
                    $"-preset fast " +
                    $"\"{videoPath}\"";

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.RedirectStandardError = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                UnityEngine.Debug.Log($"[VideoRecorder] 🎥 FFmpeg iniciado. Video: {videoPath}");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[VideoRecorder] Error iniciando FFmpeg: {ex.Message}. Usando PNG como fallback.");
                ffmpegAvailable = false;
                if (process != null && !process.HasExited)
                {
                    try { process.Kill(); } catch { }
                    process.Dispose();
                    process = null;
                }
            }
        }

        private void SendFrameToFFmpeg(Texture2D tex, ref Process process, int camIndex)
        {
            if (ffmpegAvailable && process != null && !process.HasExited)
            {
                byte[] rawData = tex.GetRawTextureData();
                byte[] frameData = flipVerticalForFfmpeg ? FlipFrameVertically(rawData) : rawData;
                try
                {
                    process.StandardInput.BaseStream.Write(frameData, 0, frameData.Length);
                    process.StandardInput.BaseStream.Flush();
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[VideoRecorder] Error escribiendo a FFmpeg (cam{camIndex}): {ex.Message}. Cambiando a PNG.");
                    ffmpegAvailable = false;
                }
            }
            else
            {
                string filename = Path.Combine(currentEpisodeFolder, $"frame_{frameCounter:D6}_cam{camIndex}.png");
                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes(filename, bytes);

                if (frameCounter % 100 == 0)
                    UnityEngine.Debug.Log($"[VideoRecorder] 📸 {frameCounter} frames PNG capturados (cam{camIndex})...");
            }
        }

        private byte[] FlipFrameVertically(byte[] source)
        {
            int rowSize = resolutionWidth * 3;
            int expectedSize = rowSize * resolutionHeight;
            if (source == null || source.Length != expectedSize)
                return source;

            if (flippedFrameBuffer == null || flippedFrameBuffer.Length != source.Length)
                flippedFrameBuffer = new byte[source.Length];

            for (int y = 0; y < resolutionHeight; y++)
            {
                int srcOffset = y * rowSize;
                int dstOffset = (resolutionHeight - 1 - y) * rowSize;
                System.Buffer.BlockCopy(source, srcOffset, flippedFrameBuffer, dstOffset, rowSize);
            }

            return flippedFrameBuffer;
        }

        private void CleanupRecordingResources()
        {
            if (activeCamera != null && activeCamera.targetTexture == camRenderTexture)
                activeCamera.targetTexture = null;

            if (RenderTexture.active == camRenderTexture)
                RenderTexture.active = null;

            if (camRenderTexture != null) { Destroy(camRenderTexture); camRenderTexture = null; }
            if (camTexture != null) { Destroy(camTexture); camTexture = null; }
            activeCamera = null;

            if (activeSecondaryCamera != null && activeSecondaryCamera.targetTexture == cam2RenderTexture)
                activeSecondaryCamera.targetTexture = null;

            if (RenderTexture.active == cam2RenderTexture)
                RenderTexture.active = null;

            if (cam2RenderTexture != null) { Destroy(cam2RenderTexture); cam2RenderTexture = null; }
            if (cam2Texture != null) { Destroy(cam2Texture); cam2Texture = null; }
            activeSecondaryCamera = null;

            flippedFrameBuffer = null;
        }

        private void OnDestroy()
        {
            if (isRecording)
                StopRecording();
        }
    }
}