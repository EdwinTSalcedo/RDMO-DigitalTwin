using System.Collections.Generic;
using UnityEngine;

public class EliminarTriangulos : MonoBehaviour
{
    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("bache"))
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshCollider meshCollider = GetComponent<MeshCollider>();

            if (meshFilter != null && meshCollider != null)
            {
                Mesh mesh = meshFilter.mesh;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;

                // Crea una lista de índices de triángulos que se eliminarán
                var indicesEliminar = new List<int>();

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 center = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f;
                    center = transform.TransformPoint(center);

                    // Verifica si el centro del triángulo está dentro del colisionador del objeto "bache"
                    if (collision.collider.bounds.Contains(center))
                    {
                        // Agrega los índices de los triángulos a eliminar a la lista
                        indicesEliminar.Add(i);
                    }
                }

                // Reemplaza los índices de los triángulos a eliminar por el índice del último vértice válido
                int ultimoVertice = vertices.Length - 1;
                foreach (int indice in indicesEliminar)
                {
                    triangles[indice] = triangles[indice + 1] = triangles[indice + 2] = ultimoVertice;
                    ultimoVertice--;
                }

                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();

                // Actualiza los triángulos del colisionador
                meshCollider.sharedMesh = mesh;
            }
        }
    }
}
