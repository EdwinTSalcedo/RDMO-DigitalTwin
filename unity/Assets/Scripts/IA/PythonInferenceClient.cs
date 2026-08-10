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
    [Tooltip("Ancho en pixeles de las capturas enviadas desde MovementInterface.")]
    public float inferenceImageWidth = 1270f;
    [Tooltip("Alto en pixeles de las capturas enviadas desde MovementInterface.")]
    public float inferenceImageHeight = 950f;
    [Tooltip("Alto de la franja horizontal central aceptada en capturas normales.")]
    public float normalCaptureCenterBandHeightPixels = 260f;
    [Tooltip("Radio maximo desde el centro de la imagen para aceptar detecciones durante revisita Skip.")]
    public float skipRevisitCenterRadiusPixels = 500f;
    [Tooltip("Limite de solicitudes simultaneas a Python para evitar acumulacion de PNGs en RAM.")]
    public int maxConcurrentRequests = 3;
    private int inFlightRequests = 0;

    // JSON response DTOs
    [Serializable]
    public class BoxData {
        public string @class;
        public string clase;
        public float cls_conf;
        public float det_conf;
        public int[] box;
        public int[] caja;

        public string ClassName => !string.IsNullOrEmpty(@class) ? @class : clase;
        public int[] BoxCoords => (box != null && box.Length > 0) ? box : caja;
    }

    [Serializable]
    public class ResponseData {
        public BoxData[] detections;
        public BoxData[] detecciones;

        public BoxData[] DetectionsList => (detections != null && detections.Length > 0) ? detections : detecciones;
    }

    void Awake()
    {
        Instance = this;
        skipRevisitCenterRadiusPixels = Mathf.Max(skipRevisitCenterRadiusPixels, 500f);

#if UNITY_WEBGL && !UNITY_EDITOR
        apiUrl = "/api/predict";
#endif
    }

    // Public method called from MovementInterface
    public void AnalyzeImageBytes(byte[] imageBytes, string imageID)
    {
        if (!CanStartRequest(imageID)) return;
        StartCoroutine(SendFrameToPython(imageBytes, imageID, "", Vector3.zero, ""));
    }

    public void AnalyzeImageBytes(byte[] imageBytes, string imageID, string candidateID, Vector3 candidateWorldPosition, string candidateTag)
    {
        if (!CanStartRequest(imageID)) return;
        StartCoroutine(SendFrameToPython(imageBytes, imageID, candidateID, candidateWorldPosition, candidateTag));
    }

    IEnumerator SendFrameToPython(byte[] imageBytes, string imageID, string candidateID, Vector3 candidateWorldPosition, string candidateTag)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", imageBytes, imageID, "image/png");

        using (UnityWebRequest www = UnityWebRequest.Post(apiUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = www.downloadHandler.text;
                try
                {
                    ParsearYDibujarCajas(jsonResponse, imageID, candidateID, candidateWorldPosition, candidateTag);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AI API -> {imageID}] Error parsing response: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"Connection error with Python while analyzing {imageID}: {www.error}");
            }
        }

        inFlightRequests = Mathf.Max(0, inFlightRequests - 1);
    }

    private bool CanStartRequest(string imageID)
    {
        if (inFlightRequests >= Mathf.Max(1, maxConcurrentRequests))
        {
            Debug.LogWarning($"[AI API -> {imageID}] Frame skipped: too many pending requests ({inFlightRequests}/{maxConcurrentRequests}).");
            return false;
        }

        inFlightRequests++;
        return true;
    }

    void ParsearYDibujarCajas(string jsonArray, string imageID, string candidateID, Vector3 candidateWorldPosition, string candidateTag)
    {
        string wrappedJson = "{\"detections\":" + jsonArray + ",\"detecciones\":" + jsonArray + "}";
        ResponseData data = JsonUtility.FromJson<ResponseData>(wrappedJson);

        var mi = DigitalTwin.DigitalTwinManager.Instance?.movementInterface;
        bool isSkipRevisit = mi != null && mi.IsInSkipSecondPass();
        string detectionID = string.IsNullOrEmpty(candidateID) ? imageID : candidateID;

        if (!isSkipRevisit && IsObstacleTag(candidateTag))
        {
            mi?.RegisterObstacleCandidateForSkip(
                detectionID,
                candidateWorldPosition,
                candidateTag,
                "AI API");
            string obstacleLabel = candidateTag == "Car" ? "car" : "person";
            Debug.Log($"[AI API -> {imageID}] {char.ToUpper(obstacleLabel[0])}{obstacleLabel.Substring(1)} detected by raycast; ignored for pothole detection.");
            return;
        }

        var detections = data?.DetectionsList;
        if (detections == null || detections.Length == 0)
        {
            Debug.Log($"[AI API -> {imageID}] No valid detections.");
            return;
        }

        BoxData firstDamageBox = null;
        BoxData centerDamageBox = null;
        float centerDistance = float.MaxValue;
        int damageOutsideCenterBand = 0;

        float imgCX = inferenceImageWidth * 0.5f;
        float imgCY = inferenceImageHeight * 0.5f;
        float centerBandHalfHeight = normalCaptureCenterBandHeightPixels * 0.5f;

        foreach (var box in detections)
        {
            string claseInf = (box.ClassName ?? "").ToLower();
            bool esBache = claseInf.Contains("pothole") || claseInf.Contains("crack") || claseInf.Contains("crocodile");
            if (!esBache) continue;

            var coords = box.BoxCoords;
            if (coords == null || coords.Length < 4) continue;

            if (isSkipRevisit)
            {
                float boxCX = (coords[0] + coords[2]) / 2f;
                float boxCY = (coords[1] + coords[3]) / 2f;
                float dist = Mathf.Sqrt((boxCX - imgCX) * (boxCX - imgCX) + (boxCY - imgCY) * (boxCY - imgCY));

                if (dist < centerDistance)
                {
                    centerDistance = dist;
                    centerDamageBox = box;
                }
            }
            else
            {
                float boxCY = (coords[1] + coords[3]) / 2f;
                bool isInsideCenterBand = Mathf.Abs(boxCY - imgCY) <= centerBandHalfHeight;

                if (isInsideCenterBand && firstDamageBox == null)
                    firstDamageBox = box;
                else if (!isInsideCenterBand)
                    damageOutsideCenterBand++;
            }
        }

        if (!isSkipRevisit && firstDamageBox == null)
        {
            if (!isSkipRevisit && damageOutsideCenterBand > 0)
                Debug.Log($"[AI API -> {imageID}] Potholes ignored (outside center band): {damageOutsideCenterBand}.");
            else
                Debug.Log($"[AI API -> {imageID}] No potholes confirmed in this frame.");
            return;
        }

        if (isSkipRevisit)
        {
            if (centerDamageBox == null || centerDistance > skipRevisitCenterRadiusPixels)
            {
                Debug.Log($"[AI API -> {imageID}] Revisit detection ignored: pothole outside center ({centerDistance:F1}px > {skipRevisitCenterRadiusPixels:F1}px).");
                return;
            }

            Debug.Log($"[AI API -> {imageID}] Skip revisit confirmed by centered pothole: {centerDamageBox.ClassName} ({centerDamageBox.cls_conf * 100:F1}%).");
            if (mi != null)
            {
                mi.RegisterSkipRevisitDiscoveredDamage(detectionID);
                mi.RegisterSkipRevisitDetection(detectionID);
                Debug.Log($"[AI API] 1 pothole recovered in Skip revisit. ID: {detectionID}");
            }
            return;
        }
        else
        {
            Debug.Log($"[AI API -> {imageID}] Pothole confirmed: {firstDamageBox.ClassName} ({firstDamageBox.cls_conf * 100:F1}%).");
        }

        if (mi != null)
        {
            mi.RegisterSegmentDetection(detectionID);
            Debug.Log($"[AI API] 1 pothole confirmed. ID: {detectionID}");
        }
    }

    private bool IsObstacleTag(string tag)
    {
        return tag == "Car" || tag == "Person";
    }

}

