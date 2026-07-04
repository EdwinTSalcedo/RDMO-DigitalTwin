using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(TerrainPotholeGenerator))]
[DisallowMultipleComponent]
public class GeneradorDeCalle : MonoBehaviour
{
    [Header("Configuración")]
    public Material calleMaterial;
    public float alturaCalle = 0.0f;
    public float tamanoSegmento = 10.0f; // Tamaño de cada segmento de calle

    private TerrainPotholeGenerator _genBaches;
    private bool _generacionIniciada = false;
    private List<GameObject> _segmentosCalle = new List<GameObject>();

    private void Awake()
    {
        _genBaches = GetComponent<TerrainPotholeGenerator>();
    }

    // Método público llamado por SceneInitializer
    public void Generate()
    {
        // Forzar reinicio si ya se había generado
        _generacionIniciada = false;
        
        // Si el generador de baches no ha terminado (y no somos nosotros quien lo llama, aunque SceneInitializer ya debió llamarlo)
        // intentamos esperar o procedemos si ya tiene hijos.
        // En este flujo, SceneInitializer llama a Baches -> wait -> Calle. Así que debería estar listo.
        if (_genBaches != null && !_genBaches.generacionTerminada)
        {
            Debug.LogWarning("[GeneradorDeCalle] Se llamó a Generate() pero TerrainPotholeGenerator dice que no ha terminado. Procediendo de todas formas con lo que haya.");
        }

        IniciarGeneracion();
        _generacionIniciada = true;
    }

    private void Update()
    {
        // Auto-arranque si NO se llama manualmente y se detecta que baches terminó
        if (!_generacionIniciada && _genBaches != null && _genBaches.generacionTerminada)
        {
            // Opcional: Descomentar si se quiere auto-generación sin SceneInitializer
            // IniciarGeneracion();
            // _generacionIniciada = true;
        }
    }

    private void IniciarGeneracion()
    {
        Debug.Log("[GeneradorDeCalle] Generando malla de calle...");

        // Limpiar segmentos anteriores si existen
        foreach (var segmento in _segmentosCalle)
        {
            if (segmento != null)
            {
                if (Application.isPlaying) Destroy(segmento);
                else DestroyImmediate(segmento);
            }
        }
        _segmentosCalle.Clear();

        // Obtener todos los baches del generador
        List<Renderer> renderersBaches = new List<Renderer>();
        if (_genBaches != null)
        {
            renderersBaches.AddRange(_genBaches.GetComponentsInChildren<Renderer>());
        }

        // Configurar área total
        float ladoTotal = _genBaches != null ? _genBaches.ladoArea : 50f;
        Vector3 centro = transform.position;
        float mitad = ladoTotal * 0.5f;
        Vector3 inicio = centro - new Vector3(mitad, 0, mitad);

        // Dividir en segmentos
        int numSegmentos = Mathf.CeilToInt(ladoTotal / tamanoSegmento);

        for (int zSeg = 0; zSeg < numSegmentos; zSeg++)
        {
            for (int xSeg = 0; xSeg < numSegmentos; xSeg++)
            {
                // Calcular límites del segmento actual
                float xMin = inicio.x + xSeg * tamanoSegmento;
                float zMin = inicio.z + zSeg * tamanoSegmento;
                float xMax = xMin + tamanoSegmento;
                float zMax = zMin + tamanoSegmento;

                // Crear segmento
                CrearSegmentoCalle(xMin, zMin, xMax, zMax, renderersBaches);
            }
        }
    }

    private void CrearSegmentoCalle(float xMin, float zMin, float xMax, float zMax, List<Renderer> baches)
    {
        // Crear listas para la malla
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangulos = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // 1. Empezar con el rectángulo completo del segmento
        List<Bounds> areasLibres = new List<Bounds>();
        areasLibres.Add(new Bounds(
            new Vector3((xMin + xMax) * 0.5f, alturaCalle, (zMin + zMax) * 0.5f),
            new Vector3(xMax - xMin, 0.1f, zMax - zMin)
        ));

        // 2. Procesar baches que intersectan con este segmento
        foreach (Renderer rend in baches)
        {
            if (rend.transform == transform) continue; // Ignorarnos a nosotros mismos si pasara

            Bounds bache = rend.bounds;
            // Expandir un poco el bounds del bache para asegurar corte limpio? No, mantener preciso.
            
            Bounds segmentoBounds = new Bounds(
                new Vector3((xMin + xMax) * 0.5f, alturaCalle, (zMin + zMax) * 0.5f),
                new Vector3(xMax - xMin, 10f, zMax - zMin) // Altura generosa para intersección
            );

            if (!segmentoBounds.Intersects(bache)) continue;

            List<Bounds> nuevasAreas = new List<Bounds>();
            foreach (Bounds area in areasLibres)
            {
                // Chequear intersección en 2D (XZ)
                // Usamos bounds completos pero nos importa X y Z
                bool intersectan = (bache.min.x < area.max.x && bache.max.x > area.min.x &&
                                    bache.min.z < area.max.z && bache.max.z > area.min.z);

                if (!intersectan)
                {
                    nuevasAreas.Add(area);
                    continue;
                }

                // Dividir el área alrededor del bache (Quadtree-like split simplificado)
                // Cortamos el área en sub-áreas rectangulares que NO toquen el bache

                // 1. Área a la IZQUIERDA del bache
                if (area.min.x < bache.min.x)
                {
                    float nuevoMaxX = bache.min.x;
                    // Área: [area.min.x, nuevoMaxX] x [area.min.z, area.max.z]
                    nuevasAreas.Add(CreateBoundsMinMax(area.min.x, nuevoMaxX, area.min.z, area.max.z, alturaCalle));
                }
                // 2. Área a la DERECHA del bache
                if (area.max.x > bache.max.x)
                {
                    float nuevoMinX = bache.max.x;
                    // Área: [nuevoMinX, area.max.x] x [area.min.z, area.max.z]
                    nuevasAreas.Add(CreateBoundsMinMax(nuevoMinX, area.max.x, area.min.z, area.max.z, alturaCalle));
                }
                // 3. Área ARRIBA (Z+) del bache (limitada a la franja X del bache para no duplicar esquinas cubiertas por izq/der)
                // La lógica anterior era: cortar todo el eje Z. 
                // Mejor estrategia: Cortar Izquierda y Derecha Completas (verticalmente), 
                // y luego cortar Arriba y Abajo SOLO en la franja central del bache.
                // O al revés. Usemos la estrategia original del script para consistencia, pero corregida.
                
                // REVISANDO LÓGICA ORIGINAL:
                // La lógica original agregaba Izquierda y Derecha recortando el ancho original?
                // No, agregaba solapamientos si no se tiene cuidado.
                // Usemos una sustracción limpia:
                // Izquierda: X [area.min, bache.min]
                // Derecha: X [bache.max, area.max]
                // Arriba: Z [bache.max, area.max] -> pero X restringido? O X completo?
                // Si usamos X completo para Izq/Der, Arriba/Abajo deben ser X [bache.min, bache.max].
                
                // Implementación Robusta:
                // Zonas Izquierda y Derecha toman toda la altura vertival (Z).
                // Zonas Arriba y Abajo toman solo el ancho del bache.
                
                // Izquierda (Toda la altura)
                if (area.min.x < bache.min.x)
                {
                   // Ya añadido arriba? No, rehagamos logicamente.
                   // No podemos usar "if" consecutivos independientes si modificamos el área.
                   // Aquí estamos creando NUEVAS áreas a partir de una vieja.
                }

                // ESTRATEGIA SIMPLE:
                // Cortar el rectangulo inicial en hasta 4 rectangulos.
                // Area Total
                // - Recorte Izquierdo (Rect completo a la izq del bache)
                // - Recorte Derecho (Rect completo a la der del bache)
                // - Recorte Arriba (Rect arriba del bache, ancho CLAMPED a los bordes del bache)
                // - Recorte Abajo (Rect abajo del bache, ancho CLAMPED a los bordes del bache)
                
                float bacheMinX = Mathf.Max(area.min.x, bache.min.x);
                float bacheMaxX = Mathf.Min(area.max.x, bache.max.x);
                float bacheMinZ = Mathf.Max(area.min.z, bache.min.z);
                float bacheMaxZ = Mathf.Min(area.max.z, bache.max.z);

                // Parte Izquierda (Full Z)
                if (area.min.x < bacheMinX)
                {
                    nuevasAreas.Add(CreateBoundsMinMax(area.min.x, bacheMinX, area.min.z, area.max.z, alturaCalle));
                }
                // Parte Derecha (Full Z)
                if (area.max.x > bacheMaxX)
                {
                    nuevasAreas.Add(CreateBoundsMinMax(bacheMaxX, area.max.x, area.min.z, area.max.z, alturaCalle));
                }
                // Parte Abajo (Z menor, X restringido al bache)
                if (area.min.z < bacheMinZ)
                {
                    nuevasAreas.Add(CreateBoundsMinMax(bacheMinX, bacheMaxX, area.min.z, bacheMinZ, alturaCalle));
                }
                // Parte Arriba (Z mayor, X restringido al bache)
                if (area.max.z > bacheMaxZ)
                {
                    nuevasAreas.Add(CreateBoundsMinMax(bacheMinX, bacheMaxX, bacheMaxZ, area.max.z, alturaCalle));
                }
            }
            areasLibres = nuevasAreas;
        }

        // 3. Generar geometría para las áreas libres
        foreach (Bounds area in areasLibres)
        {
            AddRectangleToMesh(area.min.x, area.min.z, area.max.x, area.max.z, 
                              ref vertices, ref triangulos, ref uvs);
        }

        // 4. Crear el GameObject del segmento si tiene geometría
        if (vertices.Count > 0)
        {
            GameObject segmentoGO = new GameObject($"SegmentoCalle_{xMin}_{zMin}");
            segmentoGO.transform.SetParent(transform);
            segmentoGO.transform.position = Vector3.zero; // Vertices are already World Space
            segmentoGO.transform.rotation = Quaternion.identity;
            segmentoGO.layer = 7;
            Mesh mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangulos, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter mf = segmentoGO.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = segmentoGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = calleMaterial;

            MeshCollider mc = segmentoGO.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            _segmentosCalle.Add(segmentoGO);
        }
    }

    private Bounds CreateBoundsMinMax(float minX, float maxX, float minZ, float maxZ, float y)
    {
        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;
        float centerX = minX + sizeX * 0.5f;
        float centerZ = minZ + sizeZ * 0.5f;
        return new Bounds(new Vector3(centerX, y, centerZ), new Vector3(sizeX, 0.1f, sizeZ));
    }

    private void AddRectangleToMesh(float minX, float minZ, float maxX, float maxZ,
                                  ref List<Vector3> vertices, ref List<int> triangulos, ref List<Vector2> uvs)
    {
        int start = vertices.Count;

        // Vértices (Local relative to parent? No, we are building world pos but parenting later.
        // Wait, CrearSegmentoCalle receives World coords (xMin...).
        // But we parent to 'transform' and logic implies local 0.
        // Let's make vertices strictly LOCAL to the segment GO, or make segment GO at 0,0,0 world.
        // Original code: segmentGO.transform.localPosition = Vector3.zero; -> So it was at transform.position.
        // Vertices were created using xMin which are World coords offset from start.
        // We must converting World xMin to Local if we parent!
        // FIX: Simply put segmentGO at (0,0,0) world, parent it, and keep vertices world logic?
        // OR: Make vertices relative.
        // Easier: Set segmentGO position to Vector3.zero (World), then parent.
        
        // Vértices
        vertices.Add(new Vector3(minX, alturaCalle, minZ)); // BL
        vertices.Add(new Vector3(minX, alturaCalle, maxZ)); // TL
        vertices.Add(new Vector3(maxX, alturaCalle, maxZ)); // TR
        vertices.Add(new Vector3(maxX, alturaCalle, minZ)); // BR

        // UVs
        uvs.Add(new Vector2(minX, minZ)); // World coords mapping for tiling
        uvs.Add(new Vector2(minX, maxZ));
        uvs.Add(new Vector2(maxX, maxZ));
        uvs.Add(new Vector2(maxX, minZ));

        // Triángulos
        triangulos.Add(start + 0); triangulos.Add(start + 1); triangulos.Add(start + 2);
        triangulos.Add(start + 0); triangulos.Add(start + 2); triangulos.Add(start + 3);
    }
}