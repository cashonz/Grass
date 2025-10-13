using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    Vector3[] verts;
    Vector2[] uvs;
    int[] tris;
    private int vertIndex;
    private int triIndex;
    [Header("FBm settings")]
    public int seed;
    public float scale;
    public int octaves;
    public float persistance;
    public float lacunarity;
    public Vector2 offset;
    public float heightMult;
    public bool useRandomSeed;

    public Mesh GenerateTerrain(float[,] heightMap, int width, int height, int spacingCoefficent)
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        vertIndex = 0;
        triIndex = 0;
        verts = new Vector3[width * height];
        uvs = new Vector2[width * height];
        tris = new int[(width - 1) * (height - 1) * 6];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                float xPos = (((float)x / width) - 0.5f) * spacingCoefficent;
                float zPos = (((float)z / height) - 0.5f) * spacingCoefficent;
                float yPos = heightMap[x, z] * heightMult;

                verts[vertIndex] = new Vector3(xPos, yPos, zPos);
                uvs[vertIndex] = new Vector2(x / (float)width, z / (float)height);

                if (x < width - 1 && z < height - 1)
                {
                    AddTri(vertIndex, vertIndex + width + 1, vertIndex + width);
                    AddTri(vertIndex + width + 1, vertIndex, vertIndex + 1);
                }

                vertIndex++;
            }
        }
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.name = "groundMesh";
        mesh.RecalculateNormals();

        return mesh;
    }

    public GameObject CreateTerrainObject(Mesh mesh, Material groundMat)
    {
        GameObject terrainObj = new GameObject();
        terrainObj.AddComponent<MeshFilter>();
        terrainObj.AddComponent<MeshRenderer>();
        terrainObj.GetComponent<MeshFilter>().mesh = mesh;
        terrainObj.GetComponent<MeshRenderer>().material = groundMat;
        terrainObj.name = "Ground";

        return terrainObj;
    }

    private void AddTri(int a, int b, int c)
    {
        tris[triIndex] = a;
        tris[triIndex + 1] = b;
        tris[triIndex + 2] = c;
        triIndex += 3;
    }

    public float[,] GetHeightMap(int width, int height)
    {
        PerlinfBm noise = new PerlinfBm();
        float[,] noiseData = noise.GenerateNoiseMap(width, height, seed, scale, octaves, persistance, lacunarity, offset, useRandomSeed);
        return noiseData;
    }
}
