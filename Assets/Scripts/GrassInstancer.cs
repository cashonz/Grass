using UnityEngine;

public class GrassInstancer : MonoBehaviour
{
    [Header("Grass Params")]
    public int size;
    public int spacingCoefficent;
    public float positionOffset;
    public int grassMeshScale;
    public float grassMeshScaleOffset;
    public float grassStretchFactor;
    [Range(0, 1)] public float grassHeightCutoff;
    public Color bottomColor;
    public Color topColor;
    public ColorationType colorationType;
    public enum ColorationType
    {
        Texture,
        Color,
        Noise
    }

    [Header("GrassFog Params")]
    public Color fogColor;
    [Range(0.0f, 1.0f)] public float fogDensity;
    [Range(0.0f, 10.0f)] public float fogOffset;

    [Header("Wind Params")]
    public float windStrength;
    [Range(0, 0.2f)] public float windBendFactor;
    public float noiseScale;
    public Vector2 windDirection;

    [Header("Terrain Things (more settings on TerrainGenerator)")]
    public int terrainWidth;
    public int terrainHeight;

    [Header("Misc")]
    public Mesh grassMesh;
    public Material grassMaterial;
    public TerrainGenerator terrainGenerator;
    public Material groundMat;
    private Matrix4x4[] matrices;
    private Mesh terrainMesh;
    private GameObject terrainObj;
    private bool draw = true;
    public Texture2D grassMap;
    public ComputeShader computeShader;
    public bool useGPUForGrassPos = false;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct Grass
    {
        public Vector3 position;
        public Vector4 rotation;
        public Vector3 scale;
        public Vector2 localPos;
    }

    private Grass[] grassData;

    void Start()
    {
        if (terrainWidth % size != 0 || terrainHeight % size != 0)
        {
            Debug.LogError("please make sure width and height is divisible by size, I was too lazy to handle the cases where they aren't :)");
            draw = false;
            return;
        }

        matrices = new Matrix4x4[size * size];
        grassData = new Grass[size * size];
        float[,] heightMap = terrainGenerator.GetHeightMap(terrainWidth, terrainHeight);
        terrainMesh = terrainGenerator.GenerateTerrain(heightMap, terrainWidth, terrainHeight, spacingCoefficent);
        terrainObj = terrainGenerator.CreateTerrainObject(terrainMesh, groundMat);

        if(grassMap == null)
        {
            grassMap = GetTexFromHeightMap(heightMap);
        }

        if (useGPUForGrassPos)
        {
            InitializeGrassMatricesGPU();
        }
        else
        {
            InitializeGrassMatrices();
        }
        
    }

    void Update()
    {
        if (draw)
        {
            grassMaterial.DisableKeyword("USE_COLOR");
            grassMaterial.DisableKeyword("USE_NOISE");

            switch(colorationType)
            {
                case ColorationType.Color:
                    grassMaterial.EnableKeyword("USE_COLOR");
                    break;

                case ColorationType.Noise:
                    grassMaterial.EnableKeyword("USE_NOISE");
                    break;
            }

            grassMaterial.SetFloat("_WindStrength", windStrength);
            grassMaterial.SetFloat("_WindBendFactor", windBendFactor);
            grassMaterial.SetFloat("_NoiseScale", noiseScale);
            grassMaterial.SetVector("_WindDirection", windDirection);
            grassMaterial.SetVector("_ColorTop", topColor);
            grassMaterial.SetVector("_ColorBottom", bottomColor);
            grassMaterial.SetVector("_FogColor", fogColor);
            grassMaterial.SetFloat("_FogDensity", fogDensity);
            grassMaterial.SetFloat("_FogOffset", fogOffset);
            Graphics.DrawMeshInstanced(grassMesh, 0, grassMaterial, matrices);
        } 
    }

    void InitializeGrassMatrices()
    {
        int grassToWidthRatio = terrainWidth / size;
        int grassToHeightRatio = terrainHeight / size;

        int i = 0;

        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                if (!ValidGrassPos(x, z))
                {
                    continue;
                }

                int grassToMeshX = x * grassToWidthRatio;
                int grassToMeshZ = z * grassToHeightRatio;

                Quaternion rot = Quaternion.FromToRotation(transform.up, terrainMesh.normals[grassToMeshX + grassToMeshZ * terrainWidth]);

                float grassMeshScaleUpper = grassMeshScale + grassMeshScaleOffset;
                float grassMeshScaleLower = grassMeshScale - grassMeshScaleOffset;
                float scale = Random.Range(grassMeshScaleLower, grassMeshScaleUpper);

                Vector3 pos = terrainMesh.vertices[grassToMeshX + grassToMeshZ * terrainWidth];
                float xOffset = Random.Range(-positionOffset, positionOffset);
                float zOffset = Random.Range(-positionOffset, positionOffset);
                float yOffset = grassMesh.bounds.size.y * ((float)scale / (2 / grassStretchFactor));
                pos.x += xOffset;
                pos.z += zOffset;
                pos.y += yOffset;

                Vector3 scaleVec = new Vector3(scale, scale * grassStretchFactor, scale);

                matrices[i] = Matrix4x4.TRS(pos, rot, scaleVec);
                i++;
            }
        }
    }

    private bool ValidGrassPos(int x, int z)
    {
        Vector2 uv = new Vector2(x / (float)size, z / (float)size);
        Vector2 texCoords = new Vector2(uv.x * grassMap.width, uv.y * grassMap.height);

        if (grassMap.GetPixel((int)texCoords.x, (int)texCoords.y).grayscale > grassHeightCutoff)
        {
            return false;
        }

        return true;
    }

    private void InitializeGrassMatricesGPU()
    {
        SetLocalGrassPos();
        
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Grass));

        ComputeBuffer grassDataBuffer = new ComputeBuffer(grassData.Length, stride);
        ComputeBuffer normalsBuffer = new ComputeBuffer(terrainMesh.normals.Length, sizeof(float) * 3);
        ComputeBuffer verticesBuffer = new ComputeBuffer(terrainMesh.vertices.Length, sizeof(float) * 3);

        grassDataBuffer.SetData(grassData);
        normalsBuffer.SetData(terrainMesh.normals);
        verticesBuffer.SetData(terrainMesh.vertices);

        int kernelHandle = computeShader.FindKernel("CSMain");
        setComputeParams(kernelHandle, grassDataBuffer, normalsBuffer, verticesBuffer);

        computeShader.Dispatch(kernelHandle, grassData.Length / 8, 1, 1);

        grassDataBuffer.GetData(grassData);


        for (int i = 0; i < grassData.Length; i++)
        {
            if (grassData[i].rotation.magnitude != 0) //if quaternion is of magnitude 0, the possition was not valid in computebuffer
            {
                Vector3 pos = grassData[i].position;
                Quaternion rot = new Quaternion(grassData[i].rotation.x, grassData[i].rotation.y, grassData[i].rotation.z, grassData[i].rotation.w);
                Vector3 scaleVec = grassData[i].scale;
                matrices[i] = Matrix4x4.TRS(pos, rot, scaleVec);
            }
        }

        ReleaseBuffers(grassDataBuffer, normalsBuffer, verticesBuffer);
    }

    private void setComputeParams(int kernelHandle, ComputeBuffer grassDataBuffer, ComputeBuffer normalsBuffer, ComputeBuffer verticesBuffer)
    {
        computeShader.SetBuffer(kernelHandle, "grassBuf", grassDataBuffer);
        computeShader.SetBuffer(kernelHandle, "groundNormals", normalsBuffer);
        computeShader.SetBuffer(kernelHandle, "groundVertices", verticesBuffer);
        computeShader.SetTexture(kernelHandle, "grassMap", grassMap);
        computeShader.SetFloat("size", size);
        computeShader.SetFloat("terrainWidth", terrainWidth);
        computeShader.SetFloat("terrainHeight", terrainHeight);
        computeShader.SetFloat("grassHeightCutoff", grassHeightCutoff);
        computeShader.SetFloat("scale", grassMeshScale);
        computeShader.SetFloat("scaleOffset", grassMeshScaleOffset);
        computeShader.SetFloat("positionOffset", positionOffset);
        computeShader.SetFloat("grassStretchFactor", grassStretchFactor);
        computeShader.SetFloat("grassMeshHeight", grassMesh.bounds.size.y);
    }

    private void ReleaseBuffers(ComputeBuffer grassDataBuffer, ComputeBuffer normalsBuffer, ComputeBuffer verticesBuffer)
    {
        if(grassDataBuffer != null)
        {
            grassDataBuffer.Release();
            grassDataBuffer = null;
        }
        if (normalsBuffer != null)
        {
            normalsBuffer.Release();
            normalsBuffer = null;
        }
        if(verticesBuffer != null)
        {
            verticesBuffer.Release();
            verticesBuffer = null;
        }
    }

    private void SetLocalGrassPos()
    {
        int i = 0;
        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                grassData[i].localPos = new Vector2(x, z);
                i++;
            }
        }
    }

    private Texture2D GetTexFromHeightMap(float[,] heightMap)
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[width * height];

        for (int x = 0; x < height; x++)
        {
            for (int y = 0; y < width; y++)
            {
                float pixelVal = heightMap[x, y];
                pixels[y + x * height] = new Color(pixelVal, pixelVal, pixelVal, 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return tex;
    }
}
