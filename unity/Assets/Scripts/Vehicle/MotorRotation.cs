using UnityEngine;

public class MotorRotation : MonoBehaviour
{
    [Header("Configuración")]
    public float rotationSpeed = 1000f;  // Velocidad en grados/segundo
    public bool rotateClockwise = true; // Sentido de giro
    public Transform[] motors;          // Motores a rotar (asignar en el Inspector)

    void Update()
    {
        float speed = rotationSpeed * Time.deltaTime;
        if (!rotateClockwise) speed *= -1; // Cambiar dirección si es necesario

        foreach (Transform motor in motors)
        {
            // Rotación SMOOTH y constante (en espacio local)
            motor.localRotation *= Quaternion.Euler(0, speed, 0);
            
            /* Alternativa si prefieres Rotate (también fluido):
               motor.Rotate(0, speed, 0, Space.Self); */
        }
    }
}