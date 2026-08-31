using UnityEngine;
using System.Collections.Generic;

public static class ConvexHull
{
    public static Mesh Generate(Vector3[] vertices, int numFaces)
    {
        // Crea una lista de vértices y agrega los vértices iniciales
        List<Vector3> vertexList = new List<Vector3>();
        for (int i = 0; i < vertices.Length; i++)
        {
            vertexList.Add(vertices[i]);
        }

        // Encuentra el punto más bajo y más alto
        Vector3 minPoint = vertexList[0];
        Vector3 maxPoint = vertexList[0];
        for (int i = 1; i < vertexList.Count; i++)
        {
            Vector3 point = vertexList[i];
            if (point.y < minPoint.y)
            {
                minPoint = point;
            }
            if (point.y > maxPoint.y)
            {
                maxPoint = point;
            }
        }

        // Crea una lista de caras y agrega las caras iniciales
        List<Triangle> faceList = new List<Triangle>();
        Vector3 baseLine = maxPoint - minPoint;
        Triangle tri = new Triangle(minPoint, maxPoint, baseLine);
        faceList.Add(tri);

        // Agrega los vértices restantes a las caras
        for (int i = 0; i < vertexList.Count; i++)
        {
            Vector3 point = vertexList[i];
            List<Edge> edgeList = new List<Edge>();

            // Encuentra todas las caras que son visibles desde el punto
            for (int j = 0; j < faceList.Count; j++)
            {
                Triangle face = faceList[j];
                if (face.IsVisibleFromPoint(point))
                {
                    edgeList.Add(new Edge(face.v1, face.v2));
                    edgeList.Add(new Edge(face.v2, face.v3));
                    edgeList.Add(new Edge(face.v3, face.v1));
                    faceList.RemoveAt(j);
                    j--;
                }
            }

            // Elimina los bordes duplicados
            for (int j = 0; j < edgeList.Count - 1; j++)
            {
                Edge edge1 = edgeList[j];
                for (int k = j + 1; k < edgeList.Count; k++)
                {
                    Edge edge2 = edgeList[k];
                    if ((edge1.v1 == edge2.v2 && edge1.v2 == edge2.v1) || (edge1.v1 == edge2.v1 && edge1.v2 == edge2.v2))
                    {
                        edgeList.RemoveAt(k);
                        edgeList.RemoveAt(j);
                        j--;
                        break;
                    }
                }
            }

            // Crea nuevas caras a partir del punto y los bordes visibles
            for (int j = 0; j < edgeList.Count; j++)
            {
                Edge edge = edgeList[j];
                Triangle newTri = new Triangle(point, edge.v1, edge.v2);
                faceList.Add(newTri);
            }
        }

        // Elimina todas las caras que contienen el punto más bajo o el punto más alto
            for (int i = 0; i < faceList.Count; i++)
    {
        Triangle face = faceList[i];
        if (face.ContainsPoint(minPoint) || face.ContainsPoint(maxPoint))
        {
            faceList.RemoveAt(i);
            i--;
        }
    }

    // Crea un nuevo conjunto de vértices y caras
    List<Vector3> newVertices = new List<Vector3>();
    List<int> newTriangles = new List<int>();
    for (int i = 0; i < faceList.Count; i++)
    {
        Triangle face = faceList[i];
        newVertices.Add(face.v1);
        newVertices.Add(face.v2);
        newVertices.Add(face.v3);
        newTriangles.Add(newVertices.Count - 3);
        newTriangles.Add(newVertices.Count - 2);
        newTriangles.Add(newVertices.Count - 1);
    }

    // Crea una nueva malla y la devuelve
    Mesh mesh = new Mesh();
    mesh.SetVertices(newVertices);
    mesh.SetTriangles(newTriangles, 0);
    mesh.RecalculateNormals();
    return mesh;
}

private class Triangle
{
    public Vector3 v1;
    public Vector3 v2;
    public Vector3 v3;
    public Vector3 normal;

    public Triangle(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        this.v1 = v1;
        this.v2 = v2;
        this.v3 = v3;
        normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;
    }

    public bool ContainsPoint(Vector3 point)
    {
        float d1 = Vector3.Dot(point - v1, Vector3.Cross(v2 - v1, v3 - v1));
        float d2 = Vector3.Dot(point - v2, Vector3.Cross(v3 - v2, v1 - v2));
        float d3 = Vector3.Dot(point - v3, Vector3.Cross(v1 - v3, v2 - v3));
        if (d1 >= 0f && d2 >= 0f && d3 >= 0f)
        {
            return true;
        }
        if (d1 <= 0f && d2 <= 0f && d3 <= 0f)
        {
            return true;
        }
        return false;
    }

    public bool IsVisibleFromPoint(Vector3 point)
    {
        float dot = Vector3.Dot(normal, point - v1);
        if (dot > 0f)
        {
            return true;
        }
        return false;
    }
}

private class Edge
{
    public Vector3 v1;
    public Vector3 v2;

    public Edge(Vector3 v1, Vector3 v2)
    {
        this.v1 = v1;
        this.v2 = v2;
    }

    public bool Equals(Edge other)
    {
        if ((v1 == other.v1 && v2 == other.v2) || (v1 == other.v2 && v2 == other.v1))
        {
            return true;
        }
        return false;
    }
}
}
