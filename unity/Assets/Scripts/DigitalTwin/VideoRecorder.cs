using UnityEngine;
using System.Collections;
using System.IO;
using System.Diagnostics;

namespace DigitalTwin
{
    /// <summary>
    /// Graba video directamente a MP4 usando FFmpeg por pipe (sin guardar PNGs individuales).
    /// Envía frames en crudo (bmp) por stdin de FFmpeg, que codifica a H.264 sobre la marcha.
    /// </summary>
    public class VideoRecorder : MonoBehaviour
    {
        [Header("Cámara a grabar")]
        [Tooltip("Si está vacío, busca automáticamente: hijas → propio → Camera.main")]
        public Camera targetCamera;

        [Header("Configuración de grabación")]
        public int resolutionWidth = 1280;
        public int resolutionHeight = 720;
        public int fps = 10;
        [Tooltip("Bitrate del video (mayor = mejor calidad pero más peso). 2M = bueno.")]
        public string videoBitrate = "2M";
        public string outputFolder = "DigitalTwin_Logs/Recordings";

        [Header("FFmpeg")]
        [Tooltip("Ruta al ejecutable ffmpeg. Si está en PATH, dejar 'ffmpeg'.")]
        public string ffmpegPath = "ffmpeg";

        private bool isRecording = false;
        private Coroutine recordingCoroutine;
        private Process ffmpegProcess;
        private string currentEpisodeFolder;
        private int frameCounter = 0;
        private bool ffmpegAvailable = false;

        private void Start()
        {
            // Verificar si FFmpeg está disponible
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
            {
                UnityEngine.Debug.LogWarning("[VideoRecorder] ⚠ FFmpeg no encontrado. Los frames se guardarán como PNG.");
            }
            else
            {
                UnityEngine.Debug.Log($"[VideoRecorder] ✅ FFmpeg disponible en: {ffmpegPath}");
            }
        }

        /// <summary>
        /// Inicia la grabación para un episodio específico.
        /// </summary>
        public void StartRecording(int episodeNumber)
        {
            if (isRecording)
            {
                UnityEngine.Debug.LogWarning("[VideoRecorder] Ya se está grabando. Deteniendo grabación anterior...");
                StopRecording();
            }

            // Buscar cámara
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

            UnityEngine.Debug.Log($"[VideoRecorder] 📹 Grabando con cámara: {cameraToUse.name}");

            // Crear carpeta para este episodio
            string basePath = Path.Combine(Application.dataPath, "..", outputFolder);
            currentEpisodeFolder = Path.Combine(basePath, $"Ep_{episodeNumber}");
            Directory.CreateDirectory(currentEpisodeFolder);

            frameCounter = 0;
            isRecording = true;
            recordingCoroutine = StartCoroutine(RecordingLoop(cameraToUse, episodeNumber));

            UnityEngine.Debug.Log($"[VideoRecorder] 🎬 Iniciando grabación Episodio #{episodeNumber} en: {currentEpisodeFolder}");
        }

        /// <summary>
        /// Detiene la grabación actual.
        /// </summary>
        public void StopRecording()
        {
            if (!isRecording) return;

            isRecording = false;
            if (recordingCoroutine != null)
            {
                StopCoroutine(recordingCoroutine);
                recordingCoroutine = null;
            }

            // Cerrar FFmpeg si está activo
            if (ffmpegProcess != null && !ffmpegProcess.HasExited)
            {
                try
                {
                    // Enviar 'q' para cerrar FFmpeg correctamente (o cerrar stdin)
                    ffmpegProcess.StandardInput.Close();
                    ffmpegProcess.WaitForExit(3000);
                    if (!ffmpegProcess.HasExited)
                        ffmpegProcess.Kill();
                }
                catch { }
                ffmpegProcess.Dispose();
                ffmpegProcess = null;
            }

            if (ffmpegAvailable)
            {
                UnityEngine.Debug.Log($"[VideoRecorder] ⏹ Grabación detenida. {frameCounter} frames → video MP4 en: {currentEpisodeFolder}");
            }
            else
            {
                UnityEngine.Debug.Log($"[VideoRecorder] ⏹ Grabación detenida. {frameCounter} frames PNG guardados en: {currentEpisodeFolder}");
            }
        }

        private IEnumerator RecordingLoop(Camera cameraToUse, int episodeNumber)
        {
            RenderTexture rt = new RenderTexture(resolutionWidth, resolutionHeight, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            cameraToUse.targetTexture = rt;

            Texture2D tex = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);

            string videoPath = Path.Combine(currentEpisodeFolder, $"Ep_{episodeNumber}.mp4");

            // Iniciar FFmpeg si está disponible
            if (ffmpegAvailable)
            {
                try
                {
                    ffmpegProcess = new Process();
                    ffmpegProcess.StartInfo.FileName = ffmpegPath;
                    ffmpegProcess.StartInfo.Arguments = $"-y " +
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

                    ffmpegProcess.StartInfo.UseShellExecute = false;
                    ffmpegProcess.StartInfo.RedirectStandardInput = true;
                    ffmpegProcess.StartInfo.RedirectStandardOutput = true;
                    ffmpegProcess.StartInfo.RedirectStandardError = true;
                    ffmpegProcess.StartInfo.CreateNoWindow = true;

                    ffmpegProcess.Start();

                    UnityEngine.Debug.Log($"[VideoRecorder] 🎥 FFmpeg iniciado. Video: {videoPath}");
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[VideoRecorder] Error iniciando FFmpeg: {ex.Message}. Usando PNG como fallback.");
                    ffmpegAvailable = false;
                    if (ffmpegProcess != null && !ffmpegProcess.HasExited)
                    {
                        try { ffmpegProcess.Kill(); } catch { }
                        ffmpegProcess.Dispose();
                        ffmpegProcess = null;
                    }
                }
            }

            float captureInterval = 1f / fps;

            while (isRecording)
            {
                // Capturar frame
                cameraToUse.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, resolutionWidth, resolutionHeight), 0, 0);
                tex.Apply();

                if (ffmpegAvailable && ffmpegProcess != null && !ffmpegProcess.HasExited)
                {
                    // Enviar raw pixels a FFmpeg por pipe
                    byte[] rawData = tex.GetRawTextureData();
                    try
                    {
                        ffmpegProcess.StandardInput.BaseStream.Write(rawData, 0, rawData.Length);
                        ffmpegProcess.StandardInput.BaseStream.Flush();
                    }
                    catch (System.Exception ex)
                    {
                        UnityEngine.Debug.LogError($"[VideoRecorder] Error escribiendo a FFmpeg: {ex.Message}. Cambiando a PNG.");
                        ffmpegAvailable = false;
                    }
                }
                else
                {
                    // Fallback: guardar como PNG
                    string filename = Path.Combine(currentEpisodeFolder, $"frame_{frameCounter:D6}.png");
                    byte[] bytes = tex.EncodeToPNG();
                    File.WriteAllBytes(filename, bytes);

                    if (frameCounter % 100 == 0)
                        UnityEngine.Debug.Log($"[VideoRecorder] 📸 {frameCounter} frames PNG capturados...");
                }

                frameCounter++;

                yield return new WaitForSeconds(captureInterval);
            }

            // Limpieza
            cameraToUse.targetTexture = null;
            RenderTexture.active = null;
            Destroy(rt);
            Destroy(tex);

            // Cerrar FFmpeg
            if (ffmpegProcess != null && !ffmpegProcess.HasExited)
            {
                try
                {
                    ffmpegProcess.StandardInput.Close();
                    ffmpegProcess.WaitForExit(5000);
                }
                catch { }
                ffmpegProcess.Dispose();
                ffmpegProcess = null;
            }
        }

        private void OnDestroy()
        {
            if (isRecording)
                StopRecording();
        }
    }
}