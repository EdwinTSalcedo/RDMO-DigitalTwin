using UnityEditor;
using UnityEngine;

public class WebGLBuilder
{
    public static void BuildWebGL()
    {
        Debug.Log("[WebGLBuilder] Iniciando compilación de WebGL...");

        string[] scenes = new string[]
        {
            "Assets/Scenes/Mode_Menu.unity",
            "Assets/Scenes/Mode_Load.unity",
            "Assets/Scenes/Mode_Model.unity",
            "Assets/Scenes/Mode_Data.unity",
            "Assets/Scenes/Mode_Capture.unity"
        };

        string buildPath = "../web/simulator-web";

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[WebGLBuilder] ✅ Compilación WebGL EXITOSA: {summary.totalSize} bytes");
        }
        else
        {
            Debug.LogError($"[WebGLBuilder] ❌ Compilación FALLIDA: {summary.result}");
        }
    }
}
