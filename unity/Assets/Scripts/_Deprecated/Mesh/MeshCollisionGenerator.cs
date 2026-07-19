using UnityEngine;

public class MeshCollisionGenerator : MonoBehaviour
{
    public int numColliders = 1; // número de colisionadores a generar
    public float complexity = 0.6f; // complejidad de los colisionadores generados (0-1)
    public float height = 2.0f; // altura de los colisionadores generados

    void Start()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh; // obtiene la malla del objeto

        for (int i = 0; i < numColliders; i++)
        {
            GameObject colliderObject = new GameObject("Collider " + i); // crea un objeto para el colisionador
            colliderObject.transform.parent = transform; // establece el objeto creado como hijo del objeto actual

            MeshCollider meshCollider = colliderObject.AddComponent<MeshCollider>(); // agrega un componente MeshCollider al objeto creado
            meshCollider.convex = false; // establece que el colisionador no es convexo

            Mesh colliderMesh = new Mesh(); // crea una malla para el colisionador

            // genera la malla para el colisionador utilizando una versión simplificada de la malla original
            MeshSimplifier simplifier = new MeshSimplifier();
            simplifier.SetMesh(mesh);
            simplifier.SimplifyMesh(complexity);
            
            try {
                colliderMesh = simplifier.ToMesh();
            } catch (System.Exception e) {
                Debug.Log("Error al generar el collider: " + e.Message);
                colliderMesh = null; // O puedes asignar un colliderMesh vacío o alguna otra solución que consideres adecuada.
            }


            // ajusta la altura de la malla del colisionador generado
            Vector3[] vertices = colliderMesh.vertices;
            for (int j = 0; j < vertices.Length; j++)
            {
                vertices[j].y = height;
            }
            colliderMesh.vertices = vertices;

            meshCollider.sharedMesh = colliderMesh; // establece la malla generada como la malla del colisionador
        }
    }
}
