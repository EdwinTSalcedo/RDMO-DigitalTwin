using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class MeshGenerator : MonoBehaviour {
    public List<GameObject> objetosGenerados = new List<GameObject>();
    // La imagenn de referencia
    public Texture2D imagen;

    // El material para la malla
    public Material material;

    // El tamaño de cada píxel en unidades de mundo
    public float tamañoPixel = 0.1f;

    // El umbral para determinar si un píxel es blanco o negro
    public float threshold = 0.5f;

    // El porcentaje de azul para los píxeles blancos
    [Range(0, 100)]
    public int bluePercentage = 50;

    // El componente MeshFilter
    private MeshFilter meshFilter;

    // El componente MeshRenderer
    private MeshRenderer meshRenderer;

    // El objeto Mesh
    private Mesh mesh;

    // Los vértices de la malla
    private List<Vector3> vertices;

    // Las coordenadas UV de la malla
    private List<Vector2> uvs;

    // Los triángulos de la malla
    private List<int> triangles;

    // La nueva imagen modificada
    private Texture2D newimagen;
    public float heightScale = 1.0f;
    public Material materialbache;
    public int polygonCount = 2;
    public float size = 1.0f;
    public int scale = 1;
    public int randomvalue;
    public int RangeEdgeSkip;
    public int RangeEdgeInitial;
    Vector3 objectPosition;
    void Start()
    {
        // Deshabilitado para ejecución manual
    }

    public void Generate() {
        objetosGenerados.Add(this.gameObject);
        // Obtener los componentes
        meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        meshRenderer = GetComponent<MeshRenderer>();
        objectPosition = transform.position;
        // Asignar el material
        meshRenderer.material = material;
        MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
        // Crear el objeto Mesh
        mesh = new Mesh();

        // Inicializar las listas
        vertices = new List<Vector3>();
        uvs = new List<Vector2>();
        triangles = new List<int>();

        // Generar la nueva imagen a partir de la original
        GenerateNewimagen();

        // Guardar la nueva imagen en Assets/Textures
        SaveNewimagen();

        // Generar la malla a partir de la nueva imagen
        GenerateMeshFromimagen();

        // Generar los baches a partir de la nueva imagen
        CreateBaches();

        // Asignar la malla al componente MeshFilter
        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = meshFilter.sharedMesh;
        gameObject.AddComponent<MeshCollisionGenerator>();
        

    }

    void GenerateNewimagen() {
        // Obtener el ancho y el alto de la imagen original en píxeles
        int width = imagen.width;
        int height = imagen.height;

        // Crear la nueva imagen con el mismo tamaño que la original
        newimagen = new Texture2D(width, height);

        // Recorrer cada píxel de la imagen original
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                // Obtener el valor del píxel en escala de grises
                float pixelValue = imagen.GetPixel(x, y).grayscale;

                // Si el valor es mayor que el umbral, modificar el color del píxel según el porcentaje de azul
                if (pixelValue > threshold) {
                    Color pixelColor;
                    int randomValue = Random.Range(0, 100);
                    if (randomValue < bluePercentage) {
                        pixelColor = Color.blue;
                    } else {
                        pixelColor = Color.white;
                    }
                    newimagen.SetPixel(x, y, pixelColor);
                } else {
                    // Si el valor es menor o igual que el umbral, mantener el color del píxel original
                    newimagen.SetPixel(x, y, imagen.GetPixel(x, y));
                }
            }
        }

        // Aplicar los cambios a la nueva imagen
        newimagen.Apply();
    }

    void SaveNewimagen() {
        // Obtener los bytes de la nueva imagen en formato PNG
        byte[] bytes = newimagen.EncodeToPNG();

        // Crear el directorio Assets/Textures si no existe
        Directory.CreateDirectory(Application.dataPath + "/Textures");

        // Escribir los bytes en un archivo con el nombre Newimagen.png en el directorio Assets/Textures
        File.WriteAllBytes(Application.dataPath + "/Textures/Newimagen.png", bytes);
        
        Debug.Log("Nueva imagenn guardada en Assets/Textures/Newimagen.png");
    }
    void GenerateMeshFromimagen() {
        // Obtener el ancho y el alto de la nueva imagen en píxeles
        int width = newimagen.width;
        int height = newimagen.height;
        

        // Recorrer cada píxel de la nueva imagen
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                // Obtener el valor del píxel en escala de grises
                float pixelValue = newimagen.GetPixel(x, y).grayscale;
                float xreal=x-0.5f;
                float yreal=y-0.5f;
                // Si el valor es mayor que el umbral, crear un cuadrado en la malla
                if (pixelValue > threshold) {
                    CreateSquare(xreal, yreal);
                }
            }
        }

        // Calcular el centro de la malla
        mesh.RecalculateBounds();
        Vector3 center =objectPosition;
        
        // Centrar la malla restando el centro a los vértices
        for (int i = 0; i < vertices.Count; i++) {
            vertices[i] -= center ;
        }
        center += objectPosition;
        // Asignar los vértices, las coordenadas UV y los triángulos a la malla
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();
        
        // Optimizar y recalcular las normales de la malla
        mesh.Optimize();
        mesh.RecalculateNormals();
        
    }

    void CreateSquare(float x, float y) {

    // Calcular las coordenadas del centro del cuadrado
    float centerX = (imagen.width - 1) * tamañoPixel / 2f;
    float centerZ = (imagen.height - 1) * tamañoPixel / 2f;
    float vx = (x * tamañoPixel) - centerX + transform.position.x;
    float vz = (y * tamañoPixel) - centerZ + transform.position.z;

    // Añadir los cuatro vértices del cuadrado a la lista de vértices
    vertices.Add(new Vector3(vx, 0, vz)); // inferior izquierdo
    vertices.Add(new Vector3(vx + tamañoPixel, 0, vz)); // inferior derecho
    vertices.Add(new Vector3(vx + tamañoPixel, 0, vz + tamañoPixel)); // superior derecho
    vertices.Add(new Vector3(vx, 0, vz + tamañoPixel)); // superior izquierdo

    // Añadir las cuatro coordenadas UV del cuadrado a la lista de coordenadas UV
    // Estas pueden ser las que quieras, dependiendo de cómo quieras mapear la textura
    uvs.Add(new Vector2(1, 1));
    uvs.Add(new Vector2(0, 1));
    uvs.Add(new Vector2(0, 0));
    uvs.Add(new Vector2(1, 0));

    // Añadir los seis índices de los vértices que forman los dos triángulos del cuadrado a la lista de triángulos
    // Estos deben estar en el sentido contrario a las agujas del reloj
    int vertexIndex = vertices.Count - 4; // El índice del primer vértice que hemos añadido
    triangles.Add(vertexIndex); // Primer triángulo: inferior izquierdo
    triangles.Add(vertexIndex + 3); // Primer triángulo: inferior derecho
    triangles.Add(vertexIndex + 2); // Primer triángulo: superior derecho
    triangles.Add(vertexIndex); // Segundo triángulo: inferior izquierdo
    triangles.Add(vertexIndex + 2); // Segundo triángulo: superior derecho
    triangles.Add(vertexIndex + 1); // Segundo triángulo: superior izquierdo
}
void CreateBaches()
    {
        int width = Mathf.RoundToInt(Mathf.Sqrt(polygonCount));
        int length = width;
        // Creamos un objeto padre para los baches
        GameObject bachesTotales = new GameObject("Baches totales");
        
        // Recorremos todos los píxeles de la imagen
        for (int x = 0; x < imagen.width; x++)
        {
            for (int y = 0; y < imagen.height; y++)
            {

                int edgeSkip = Random.Range(RangeEdgeInitial, RangeEdgeSkip + 1);
                // Obtenemos el color del pixel
                Color pixel = newimagen.GetPixel(x, y);
                Debug.Log(pixel);
                // Si es azul, generamos un objeto bache
                if (pixel == Color.blue)
                {
                    Debug.Log("Holas wew");
                    // Creamos un objeto hijo para el bache
                    GameObject bache = new GameObject("Bache_" + (bachesTotales.transform.childCount + 1));
                    objetosGenerados.Add(bache);
                    bache.tag = "bache";
                    bache.transform.parent = bachesTotales.transform;
                    bache.transform.position = new Vector3((x - imagen.width / 2) * tamañoPixel, 0, (y - imagen.height / 2) * tamañoPixel);

                    MeshFilter mf = bache.AddComponent<MeshFilter>();
                    MeshRenderer mr = bache.AddComponent<MeshRenderer>();
                    MeshCollider meshColliderbache = bache.AddComponent<MeshCollider>();
                    Mesh mesh = new Mesh();
                    mf.mesh = mesh;

                    // Crear un nuevo material y asignar la textura
                    

                    int edgeSkipactual = edgeSkip;
                    Vector3[] vertices = new Vector3[width * length];
                    float maxHeight = float.MinValue;
                    float minHeight = float.MaxValue;

                    // Generar los vértices del mesh
                    int maxLength = Mathf.Max(length, width);
                    for (int d = 0; d < maxLength * 2 - 1; d++)
                    {
                        int z = Mathf.Min(d, length - 1);
                        int v = Mathf.Max(0, d - length + 1);
                        
                        while (z >= 0 && v < width)
                        {
                            float w = 0;
                            if (v < edgeSkipactual || v >= width - edgeSkipactual || z < edgeSkipactual || z >= length - edgeSkipactual)
                            {
                                vertices[z * width + v] = new Vector3((float)v / (width - 1) * size, 0.0f, (float)z / (length - 1) * size);
                            }
                            else
                            {
                                float noise = Mathf.PerlinNoise((float)v / (width - 1) * scale + transform.position.x, (float)z / (length - 1) * scale + transform.position.z);
                                w = -noise * heightScale;
                                vertices[z * width + v] = new Vector3((float)v / (width - 1) * size, w, (float)z / (length - 1) * size);
                            }

                            if (w > maxHeight)
                            {
                                maxHeight = w;
                            }
                            if (w < minHeight)
                            {
                                minHeight = w;
                            }
                            int range = Random.Range(1, randomvalue);
                            if (range % 4 == 0)
                            {
                                int randomNumber = Random.Range(1, edgeSkip + 1);
                                edgeSkipactual = randomNumber;
                            }

                            z--;
                            v++;
                        }
                    }



                    
                    mesh.vertices = vertices;

                    // Generar los normales del mesh
                    mesh.RecalculateNormals();

                    // Crear los triángulos del mesh
                    int[] triangles = new int[(width - 1) * (length - 1) * 6];
                    int ti = 0;
                    int vi = 0;
                    for (int m = 0; m < length - 1; m++, vi++)
                    {
                        for (int n = 0; n < width - 1; n++, ti += 6, vi++)
                        {
                            triangles[ti] = vi;
                            triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                            triangles[ti + 4] = triangles[ti + 1] = vi + width;
                            triangles[ti + 5] = vi + width + 1;
                        }
                    }

                    mesh.triangles = triangles;
                    mesh.RecalculateNormals();
                    // Asignar el mesh a los componentes MeshCollider y MeshCollisionGenerator
                    meshColliderbache.sharedMesh = mf.sharedMesh;
                    bache.AddComponent<MeshCollisionGenerator>();
                    MeshFilter meshFilterbache = bache.GetComponent<MeshFilter>();
                    Mesh meshbache = meshFilterbache.mesh;

                    // Asignar el mesh al MeshFilter del objeto bache
                    meshFilterbache.mesh = mesh;

                    // Asignar el mesh al MeshCollider del objeto bache
                    meshColliderbache.sharedMesh = mesh;

                    // Añadir el componente MeshCollisionGenerator al objeto bache
                    bache.AddComponent<MeshCollisionGenerator>();
                    
                    // Generar las coordenadas UV del mesh
                    Vector2[] uv = new Vector2[width * length];
                    for (int z = 0, i = 0; z < length; z++)
                    {
                        for (int v = 0; v < width; v++, i++)
                        {
                            uv[i] = new Vector2((float)v / (width - 1), (float)z / (length - 1));
                        }
                    }
                    
                    mesh.uv = uv;

                    mr.material = materialbache;
                    Debug.Log("Longitud de meshbache.normals: " + meshbache.normals.Length);
                    Debug.Log("Longitud de meshbache.vertices: " + meshbache.vertices.Length);
    
                }
            }
        }
        bachesTotales.transform.position = objectPosition-new Vector3(0,0,size/2);
    }
}
