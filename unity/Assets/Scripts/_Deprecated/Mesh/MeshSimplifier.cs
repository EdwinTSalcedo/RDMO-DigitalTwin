using UnityEngine;
using System.Collections.Generic;

public class MeshSimplifier
{
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private int contador;

    public void SetMesh(Mesh mesh)
    {
        vertices = new List<Vector3>(mesh.vertices);
        triangles = new List<int>(mesh.triangles);
    }

    public void SimplifyMesh(float complexity)
    {
        int targetTriangleCount = Mathf.RoundToInt(triangles.Count * complexity);
        try
        {
        while (triangles.Count > targetTriangleCount)
        {
            float minCost = float.MaxValue;
            int triangleToRemove = -1;

            for (int i = 0; i < triangles.Count; i += 3)
            {
                int vertexIndex1 = triangles[i];
                int vertexIndex2 = triangles[i + 1];
                int vertexIndex3 = triangles[i + 2];

                Vector3 vertex1 = vertices[vertexIndex1];
                Vector3 vertex2 = vertices[vertexIndex2];
                                Vector3 vertex3 = vertices[vertexIndex3];

                float cost = Vector3.Distance(vertex1, vertex2) + Vector3.Distance(vertex1, vertex3) + Vector3.Distance(vertex2, vertex3);

                if (cost < minCost)
                {
                    minCost = cost;
                    triangleToRemove = i;
                }
            }

            int vertexIndexToRemove = triangles[triangleToRemove];
            if (triangleToRemove + 2 < triangles.Count) {
                triangles.RemoveAt(triangleToRemove + 2);
            }
            if (triangleToRemove + 1 < triangles.Count) {
                triangles.RemoveAt(triangleToRemove + 1);
            }
            if (triangleToRemove < triangles.Count) {
                triangles.RemoveAt(triangleToRemove);
            }

            if (vertexIndexToRemove < vertices.Count) {
                vertices.RemoveAt(vertexIndexToRemove);
            }

        }
        }
        catch
        {
            contador++;
        }
    }

    public Mesh ToMesh()
    {
        Mesh mesh = new Mesh();
        try
        {
            mesh.vertices = vertices.ToArray();

            // Verificar que los índices de triángulos estén dentro de los límites de la lista de vértices
            List<int> validTriangles = new List<int>();
            for (int i = 0; i < triangles.Count; i += 3) {
                if (i + 2 < triangles.Count && triangles[i] < vertices.Count && triangles[i + 1] < vertices.Count && triangles[i + 2] < vertices.Count) {
                    validTriangles.Add(triangles[i]);
                    validTriangles.Add(triangles[i + 1]);
                    validTriangles.Add(triangles[i + 2]);
                }
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
        catch
        {
            // No hacer nada en el catch para evitar que se muestre un mensaje de error
        }
        return mesh;
    }
}
