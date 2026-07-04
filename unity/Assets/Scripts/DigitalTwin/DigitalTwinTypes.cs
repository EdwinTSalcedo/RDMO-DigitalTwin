using UnityEngine;

namespace DigitalTwin
{
    // 4. Agentes Dinámicos (A_t)
    public enum AgentType { Vehicle, Pedestrian }

    [System.Serializable]
    public class TrafficAgent
    {
        public Vector3 position; // y_i^t
        public Vector3 velocity; // \nu_i^t
        public AgentType type;   // \kappa_i
    }

    // 3. Estado del UAV (x_t)
    public struct UAVState
    {
        public Vector2 p; // Posición plana (x, z)
        public float h;   // Altitud
        public float v;   // Velocidad escalar
        public float e;   // Batería [0, 1]
    }

    // 5. Memoria de inspección (m_s^t)
    public enum SegmentStatus { Pending, Inspected }

    // 6. Políticas de Recuperación (\Pi)
    // Se añadió "None" para cuando el dron está operando normalmente.
    public enum RecoveryPolicy { None, Hover, Micro, Skip }

    // 2. Segmentos de Carretera (S)
    public class RoadSegment : MonoBehaviour
    {
        public string id;
        public Vector3 center;
        public Bounds bounds;
        public float length;
        public float width;
        
        [HideInInspector]
        public float visibilityScore; // o_{s,t}
    }
}
