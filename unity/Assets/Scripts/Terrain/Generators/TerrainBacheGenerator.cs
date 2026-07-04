using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Utilidades para generar ruido procedural 2D (Perlin + FBM).
/// </summary>
public static class NoiseUtils
{
    public static float Fbm2D(float x, float y, int octaves = 3, float lacunarity = 2f, float gain = 0.5f)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        for (int i = 0; i < octaves; i++)
        {
            value += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }
        return value;
    }

    public static Vector2 VectorNoise2D(float x, float y, int seedOffset = 0)
    {
        float angle = Fbm2D(x + seedOffset, y + seedOffset, 2, 2f, 0.6f) * Mathf.PI * 2f;
        float mag = Fbm2D(x + seedOffset + 100, y + seedOffset + 200, 2, 2f, 0.5f);
        return new Vector2(Mathf.Cos(angle) * mag, Mathf.Sin(angle) * mag);
    }
}

/// <summary>
/// Generador de terreno SOLO en modo aleatorio, controlado por semilla.
/// </summary>
[ExecuteInEditMode]
public class TerrainBacheGenerator : MonoBehaviour
{
    private GameObject terreno;

    [Header("Main")]
    public int Seed = 12345;
    public bool autoUpdate = true;

    [Header("Dimension Ranges")]
    public float minWidth = 1.5f; public float maxWidth = 2.5f;
    public float minLength = 1.5f; public float maxLength = 2.5f;

    [Header("Random Configuration")]
    public int cantidadBachesAleatorios = 10;
    public float minRadioPorcentaje = 3f, maxRadioPorcentaje = 15f;
    public float minProfundidad = 0.05f, maxProfundidad = 0.2f;
    public float minDeformacion = 0.2f, maxDeformacion = 0.6f;
    public float minIrregularidadBorde = 0.4f, maxIrregularidadBorde = 0.9f;
    public float minFondoPlano = 0.2f, maxFondoPlano = 0.6f;
    public float minVariacionProf = 0.3f, maxVariacionProf = 0.7f;

    [Header("Resolution & Border")]
    [Range(20, 200)] public int polygonsX = 100;
    [Range(20, 200)] public int polygonsZ = 100;
    public float bordeMin = 0.1f, bordeMax = 0.3f;
    public float noiseScale = 2f;
    [Range(0.1f, 5f)] public float bordeSuavidad = 1.5f;

    [Range(0.01f, 1f)] public float profundidadMaximaGlobal = 0.2f;

    public Material material;
    public string nombreObjeto = "TerrenoConBaches";

    // Internal Bache structure for generation loop
    private struct BacheInfo {
        public Vector2 pos;
        public float rad, prof, def, irreg, plano, varProf, suav;
        public int seed;
    }

    private void OnValidate()
    {
        if (autoUpdate)
        {
#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                    GenerarMeshEnNuevoObjeto();
            };
#endif
        }
    }

    [ContextMenu("Generar")]
    public void GenerarMeshEnNuevoObjeto()
    {
        // Init Seed
        Random.InitState(Seed);
        
        // Randomized dimensions
        float width = Random.Range(minWidth, maxWidth);
        float length = Random.Range(minLength, maxLength);

        // Cleanup
        if (terreno != null)
        {
            if(Application.isPlaying) Destroy(terreno);
            else DestroyImmediate(terreno);
        }

        terreno = new GameObject(nombreObjeto);
        terreno.transform.SetParent(transform, false);
        terreno.transform.localPosition = Vector3.zero;
        terreno.layer = 7;

        var mf = terreno.AddComponent<MeshFilter>();
        var mr = terreno.AddComponent<MeshRenderer>();
        var mc = terreno.AddComponent<MeshCollider>();
        if (material != null) mr.sharedMaterial = material;

        Mesh mesh = new Mesh { name = "Mesh_" + nombreObjeto };

        float stepX = width / polygonsX;
        float stepZ = length / polygonsZ;
        float halfW = width * 0.5f;
        float halfL = length * 0.5f;
        float avgSize = (width + length) * 0.5f;

        // Border Noise Precalc
        float[] bordeIzq = new float[polygonsZ + 1];
        float[] bordeDer = new float[polygonsZ + 1];
        float[] bordeDet = new float[polygonsX + 1];
        float[] bordeFre = new float[polygonsX + 1];

        for (int z = 0; z <= polygonsZ; z++) {
            float t = (float)z / polygonsZ;
            bordeIzq[z] = Mathf.Lerp(bordeMin, bordeMax, Mathf.PerlinNoise(0.1f, t * noiseScale));
            bordeDer[z] = Mathf.Lerp(bordeMin, bordeMax, Mathf.PerlinNoise(10.73f, t * noiseScale));
        }
        for (int x = 0; x <= polygonsX; x++) {
            float t = (float)x / polygonsX;
            bordeDet[x] = Mathf.Lerp(bordeMin, bordeMax, Mathf.PerlinNoise(t * noiseScale, 0.3f));
            bordeFre[x] = Mathf.Lerp(bordeMin, bordeMax, Mathf.PerlinNoise(t * noiseScale, 15.41f));
        }

        // Generate Random Baches List based on Seed
        List<BacheInfo> baches = new List<BacheInfo>();
        float mP = Mathf.Max(bordeMax/width, bordeMax/length) * 100f;
        mP = Mathf.Clamp(mP, 5f, 20f);
        float minP = mP, maxP = 100f - mP;

        for(int i=0; i<cantidadBachesAleatorios; i++) {
            baches.Add(new BacheInfo {
                pos = new Vector2(Random.Range(minP, maxP), Random.Range(minP, maxP)),
                rad = Random.Range(minRadioPorcentaje, maxRadioPorcentaje),
                prof = Random.Range(minProfundidad, maxProfundidad),
                def = Random.Range(minDeformacion, maxDeformacion),
                irreg = Random.Range(minIrregularidadBorde, maxIrregularidadBorde),
                plano = Random.Range(minFondoPlano, maxFondoPlano),
                varProf = Random.Range(minVariacionProf, maxVariacionProf),
                suav = Random.Range(1f, 3f),
                seed = Random.Range(1000, 100000)
            });
        }

        Vector3[] vertices = new Vector3[(polygonsX + 1) * (polygonsZ + 1)];
        int index = 0;

        for (int z = 0; z <= polygonsZ; z++)
        {
            float localZ = z * stepZ;
            float worldZ = localZ - halfL;
            for (int x = 0; x <= polygonsX; x++)
            {
                float localX = x * stepX;
                float worldX = localX - halfW;

                float dLeft = localX - bordeIzq[z];
                float dRight = (width - bordeDer[z]) - localX;
                float dBack = localZ - bordeDet[x];
                float dFront = (length - bordeFre[x]) - localZ;
                float minD = Mathf.Min(dLeft, dRight, dBack, dFront);
                float borderFactor = 0f;
                if (minD > 0f) {
                    float t = Mathf.Clamp01(minD / (Mathf.Max(width, length) * 0.5f) * bordeSuavidad);
                    borderFactor = 1f - (1f - t) * (1f - t);
                }

                float suma = 0f;
                if (borderFactor > 0f)
                {
                    foreach (var b in baches)
                    {
                        float profUsable = Mathf.Min(b.prof, profundidadMaximaGlobal);
                        float bx = (b.pos.x * 0.01f) * width - halfW;
                        float bz = (b.pos.y * 0.01f) * length - halfL;
                        float radM = avgSize * (b.rad * 0.01f);
                        float dx = worldX - bx;
                        float dz = worldZ - bz;
                        float dist = Mathf.Sqrt(dx*dx + dz*dz);

                        if(dist >= radM && b.irreg <= 0f) continue;

                        float radEff = radM;
                        if(b.irreg > 0f && dist > 0.001f) {
                            float ang = Mathf.Atan2(dz, dx);
                            float n1 = Mathf.PerlinNoise(ang*1.3f + b.seed*0.1f, 0f);
                            float n2 = Mathf.PerlinNoise(ang*2.7f + b.seed*0.1f + 10f, 0f);
                            float n3 = Mathf.PerlinNoise(ang*5.1f + b.seed*0.1f + 20f, 0f);
                            float val = n1*0.6f + n2*0.3f + n3*0.1f;
                            float fac = 1f + (val - 0.5f) * 2f * b.irreg;
                            radEff = radM * Mathf.Max(0.3f, fac);
                        }

                        if(dist < radEff) {
                            Vector2 disp = Vector2.zero;
                            if(b.def > 0f) {
                                float s = 5f/Mathf.Max(radM, 0.01f);
                                Vector2 nv = NoiseUtils.VectorNoise2D(dx*s, dz*s, b.seed); // Global Utils
                                disp = nv * b.def * radM;
                            }
                            Vector2 pr = new Vector2(dx, dz) - disp;
                            float dd = pr.magnitude;
                            float t = dd / radEff;
                            if(t < 1f) {
                                float fP = 1f;
                                if(b.varProf > 0f) {
                                    float sp = 8f/Mathf.Max(radM, 0.01f);
                                    float np = NoiseUtils.Fbm2D(dx*sp, dz*sp, 4, 2f, 0.5f);
                                    fP = 1f + (np - 0.5f) * 2f * b.varProf;
                                    fP = Mathf.Clamp(fP, 0f, 1.5f);
                                }
                                float fS = (t<=b.plano) ? 1f : 1f - Mathf.Pow((t-b.plano)/(1f-b.plano), b.suav);
                                suma += profUsable * fS * fP;
                            }
                        }
                    }
                    suma = Mathf.Min(suma, profundidadMaximaGlobal);
                }
                vertices[index++] = new Vector3(worldX, -suma * borderFactor, worldZ);
            }
        }

        int[] tris = new int[polygonsX * polygonsZ * 6];
        int ti = 0;
        for (int z = 0; z < polygonsZ; z++) {
            for (int x = 0; x < polygonsX; x++) {
                int v = z * (polygonsX + 1) + x;
                tris[ti++] = v; tris[ti++] = v + polygonsX + 1; tris[ti++] = v + 1;
                tris[ti++] = v + 1; tris[ti++] = v + polygonsX + 1; tris[ti++] = v + polygonsX + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;
    }
}
