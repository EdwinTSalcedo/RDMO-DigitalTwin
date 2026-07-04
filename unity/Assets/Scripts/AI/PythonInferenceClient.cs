using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

public class PythonInferenceClient : MonoBehaviour
{
    public static PythonInferenceClient Instance;

    [Tooltip("URL del servidor Python")]
    public string apiUrl = "http://127.0.0.1:5000/predict";
    [Tooltip("Alto de la franja horizontal central aceptada en capturas normales.")]
    public float normalCaptureCenterBandHeightPixels = 260f;
    [Tooltip("Radio maximo desde el centro de la imagen para aceptar detecciones durante revisita Skip.")]
    public float skipRevisitCenterRadiusPixels = 180f;

    // Clases para parsear el JSON
    [Serializable]
    public class BoxData {
        public string clase;
        public float cls_conf;  // Confianza del clasificador (desde Python)
        public float det_conf;  // Confianza de detecciÃ³n YOLO
        public int[] caja; // [x1, y1, x2, y2]
    }

    [Serializable]
    public class ResponseData {
        public BoxData[] detecciones;
    }

    void Awake()
    {
        Instance = this;
    }

    // MÃ©todo pÃºblico para ser llamado desde MovementInterface
    public void AnalyzeImageBytes(byte[] imageBytes, string imageID)
    {
        StartCoroutine(SendFrameToPython(imageBytes, imageID, "", Vector3.zero, ""));
    }

    public void AnalyzeImageBytes(byte[] imageBytes, string imageID, string candidateID, Vector3 candidateWorldPosition, string candidateTag)
    {
        StartCoroutine(SendFrameToPython(imageBytes, imageID, candidateID, candidateWorldPosition, candidateTag));
    }

    IEnumerator SendFrameToPython(byte[] imageBytes, string imageID, string candidateID, Vector3 candidateWorldPosition, string candidateTag)
    {
        // Crear formulario Multipart
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", imageBytes, imageID, "image/png");

        // Enviar peticiÃ³n POST a Python
        using (UnityWebRequest www = UnityWebRequest.Post(apiUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = www.downloadHandler.text;
                ParsearYDibujarCajas(jsonResponse, imageID, candidateID, candidateWorldPosition, candidateTag);
            }
            else
            {
                Debug.LogWarning($"Error de conexiÃ³n con Python al analizar {imageID}: {www.error}");
            }
        }
    }

    void ParsearYDibujarCajas(string jsonArray, string imageID, string candidateID, Vector3 candidateWorldPosition, string candidateTag)
    {
        string wrappedJson = "{\"detecciones\":" + jsonArray + "}";
        ResponseData data = JsonUtility.FromJson<ResponseData>(wrappedJson);

        if (data.detecciones == null || data.detecciones.Length == 0)
        {
            Debug.Log($"[IA API -> {imageID}] Sin detecciones validas.");
            QueueSkipRevisitIfMissedExpectedDamage(candidateID, candidateWorldPosition, candidateTag);
            return;
        }

        var mi = DigitalTwin.DigitalTwinManager.Instance?.movementInterface;
        bool isSkipRevisit = mi != null && mi.IsInSkipSecondPass();
        string detectionID = string.IsNullOrEmpty(candidateID) ? imageID : candidateID;

        BoxData firstDamageBox = null;
        BoxData centerDamageBox = null;
        float centerDistance = float.MaxValue;
        int damageOutsideCenterBand = 0;

        float imgCX = 320f;
        float imgCY = 320f;
        float centerBandHalfHeight = normalCaptureCenterBandHeightPixels * 0.5f;

        foreach (var box in data.detecciones)
        {
            string claseInf = box.clase.ToLower();
            bool esBache = claseInf.Contains("pothole") || claseInf.Contains("crack") || claseInf.Contains("crocodile");
            if (!esBache) continue;

            if (isSkipRevisit)
            {
                float boxCX = (box.caja[0] + box.caja[2]) / 2f;
                float boxCY = (box.caja[1] + box.caja[3]) / 2f;
                float dist = Mathf.Sqrt((boxCX - imgCX) * (boxCX - imgCX) + (boxCY - imgCY) * (boxCY - imgCY));

                if (dist < centerDistance)
                {
                    centerDistance = dist;
                    centerDamageBox = box;
                }
            }
            else
            {
                float boxCY = (box.caja[1] + box.caja[3]) / 2f;
                bool isInsideCenterBand = Mathf.Abs(boxCY - imgCY) <= centerBandHalfHeight;

                if (isInsideCenterBand && firstDamageBox == null)
                    firstDamageBox = box;
                else if (!isInsideCenterBand)
                    damageOutsideCenterBand++;
            }
        }

        if (!isSkipRevisit && firstDamageBox == null)
        {
            QueueSkipRevisitIfMissedExpectedDamage(candidateID, candidateWorldPosition, candidateTag);
            if (!isSkipRevisit && damageOutsideCenterBand > 0)
                Debug.Log($"[IA API -> {imageID}] Baches ignorados por estar fuera de la franja central: {damageOutsideCenterBand}.");
            else
                Debug.Log($"[IA API -> {imageID}] No se confirmo ningun bache en esta imagen.");
            return;
        }

        if (isSkipRevisit)
        {
            if (centerDamageBox == null || centerDistance > skipRevisitCenterRadiusPixels)
            {
                Debug.Log($"[IA API -> {imageID}] Deteccion ignorada en revisita Skip: bache fuera del centro ({centerDistance:F1}px > {skipRevisitCenterRadiusPixels:F1}px).");
                QueueSkipRevisitIfMissedExpectedDamage(candidateID, candidateWorldPosition, candidateTag);
                return;
            }

            Debug.Log($"[IA API -> {imageID}] Revisita Skip confirmada por bache centrado: {centerDamageBox.clase} ({centerDamageBox.cls_conf * 100:F1}%).");
        }
        else
        {
            Debug.Log($"[IA API -> {imageID}] Bache confirmado: {firstDamageBox.clase} ({firstDamageBox.cls_conf * 100:F1}%).");
        }

        if (mi != null)
        {
            mi.RegisterSegmentDetection(detectionID);
            Debug.Log($"[IA API] 1 bache confirmado. ID: {detectionID}");
        }
    }

    void QueueSkipRevisitIfMissedExpectedDamage(string candidateID, Vector3 candidateWorldPosition, string candidateTag)
    {
        bool expectedDamage =
            candidateTag == "Pothole" ||
            candidateTag == "Crack" ||
            candidateTag == "Crack_Single" ||
            candidateTag == "Crocodile";

        if (!expectedDamage) return;

        var mi = DigitalTwin.DigitalTwinManager.Instance?.movementInterface;
        if (mi == null || mi.droneController == null) return;

        mi.droneController.QueueSkipRevisitPosition(
            candidateWorldPosition,
            $"IA no confirmÃƒÂ³ {candidateTag} {candidateID}");
    }
}

