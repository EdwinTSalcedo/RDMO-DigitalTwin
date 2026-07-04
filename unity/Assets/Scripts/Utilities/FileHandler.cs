using UnityEngine;
using System.IO;
using System.Runtime.InteropServices;

public static class FileHandler
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void DownloadFileFromBase64(string fileName, string base64);
#endif
    
    private static string currentSessionFolder = "";
    private static bool useCustomFolder = false;
    private static string customFolderPath = "";

    /// <summary>
    /// Establece una carpeta personalizada para guardar los archivos temporalmente.
    /// Si no se llama, usa la ruta por defecto (persistentDataPath).
    /// </summary>
    public static void SetCustomFolder(string folderPath)
    {
        useCustomFolder = !string.IsNullOrEmpty(folderPath);
        if (useCustomFolder)
        {
            customFolderPath = folderPath;
            if (!Directory.Exists(customFolderPath))
            {
                Directory.CreateDirectory(customFolderPath);
            }
            Debug.Log($"<color=yellow>[FileHandler] Carpeta personalizada configurada: {customFolderPath}</color>");
        }
    }

    public static string GetCurrentFolderPath()
    {
        if (useCustomFolder && !string.IsNullOrEmpty(customFolderPath))
        {
            return customFolderPath;
        }

        if (string.IsNullOrEmpty(currentSessionFolder))
        {
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            currentSessionFolder = Path.Combine(Application.persistentDataPath, "Dataset_Baches", timestamp);
            if (!Directory.Exists(currentSessionFolder)) Directory.CreateDirectory(currentSessionFolder);
        }
        return currentSessionFolder;
    }

    public static void ResetToDefaultFolder()
    {
        useCustomFolder = false;
        customFolderPath = "";
        currentSessionFolder = "";
        Debug.Log("<color=yellow>[FileHandler] Ruta restaurada a predeterminada</color>");
    }

    public static void SaveImage(byte[] pngBytes, string filename)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: Trigger browser download
        string b64 = System.Convert.ToBase64String(pngBytes);
        DownloadFileFromBase64(filename, b64);
#else
        // PC / Android: Save to disk
        string folderPath = GetCurrentFolderPath();
        string fullPath = Path.Combine(folderPath, filename);
        File.WriteAllBytes(fullPath, pngBytes);
        Debug.Log($"File saved to: {fullPath}");
#endif
    }

    public static void SaveText(string content, string filename)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // For WebGL we'd need another JSLib call, but assuming PC/Editor for training
#else
        string folderPath = GetCurrentFolderPath();
        string fullPath = Path.Combine(folderPath, filename);
        File.WriteAllText(fullPath, content);
        Debug.Log($"Label saved: {fullPath}");
#endif
    }

    public static void SaveAnnotation(string content, string filename)
    {
        SaveText(content, filename);
    }
}
