using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.IO;

public class ProceduralRockGenerator : MonoBehaviour
{
	public ComputeShader ScalarFieldCS;
	public ComputeShader TriangulationCS;
	public Shader TriangulationPS;
	public Shader SurfaceShader;
	public Texture2D SurfaceTexture;
	public int MaxVertexCount = 1024*1024*10;
	[Range(8, 512)]
	public int Resolution = 100;
	[Range(0.1f, 5.0f)]
	public float Scale = 2.5f;
	[Range(8, 72)]
	public int Steps = 20;
	[Range(0.01f, 0.2f)]
	public float Smoothness = 0.05f;
	[Range(0.0f, 1000.0f)]
	public float Seed = 880.0f;
	[Range(0.0f, 1.0f)]
	public float DisplacementScale = 0.15f;
	[Range(1.0f, 10.0f)]
	public float DisplacementSpread = 10.0f;
	public bool ShowNormals = true;
	public bool Wireframe = false;
	public bool GeneratePerTriangleUV = true;

	Material _Material;
	RenderTexture _VolumeTexture;
	ComputeBuffer _TriangleBuffer;
	ComputeBuffer _IndirectBuffer;
	ComputeBuffer _CounterBuffer;
	int _CurrentResolution;
	string _Hash { get {return System.Guid.NewGuid().ToString("N");} }

	struct Point
	{
		public Vector3 position;
		public Vector3 normal;
		public Vector3 color;
	};

	void GenerateVolumeTexture(int resolution)
	{
		RenderTextureDescriptor rtd = new RenderTextureDescriptor(Resolution, Resolution, RenderTextureFormat.RFloat);
		rtd.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
		rtd.volumeDepth = resolution; 
		_VolumeTexture = new RenderTexture(rtd);
		_VolumeTexture.enableRandomWrite = true;
		_VolumeTexture.Create();
	}

	void Start()
	{
		_Material = new Material (TriangulationPS);
		GenerateVolumeTexture(Resolution);
		_CurrentResolution = Resolution;
		_TriangleBuffer = new ComputeBuffer(MaxVertexCount, sizeof(float) * 27, ComputeBufferType.Append);
		_IndirectBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
		_CounterBuffer = new ComputeBuffer(1, 4, ComputeBufferType.Counter);
		TriangulationCS.SetFloat("_Threshold", 0.5f);
	}

	void CheckPath (string directory)
	{
		if(!Directory.Exists(Path.Combine(Application.dataPath, directory)))
		{
			Directory.CreateDirectory(Path.Combine(Application.dataPath, directory));
		}
	}

	Vector2[] CubeProjection (List<Vector3> vertices, List<int> triangles)
	{
		Vector2[] uvs = new Vector2[vertices.Count];
		for (int i = 0; i < triangles.Count; i += 3)
		{
			Vector3 a = vertices[triangles[i + 0]] / Scale + new Vector3(0.5f, 0.5f, 0.5f);
			Vector3 b = vertices[triangles[i + 1]] / Scale + new Vector3(0.5f, 0.5f, 0.5f);
			Vector3 c = vertices[triangles[i + 2]] / Scale + new Vector3(0.5f, 0.5f, 0.5f);
			Vector3 n = Vector3.Cross(b - a, c - a);
			n = new Vector3(Mathf.Abs(n.normalized.x), Mathf.Abs(n.normalized.y), Mathf.Abs(n.normalized.z));
			if (n.x > n.y && n.x > n.z)
			{
				uvs[triangles[i + 0]] = new Vector2(a.z, a.y);
				uvs[triangles[i + 1]] = new Vector2(b.z, b.y);
				uvs[triangles[i + 2]] = new Vector2(c.z, c.y);
			}
			else if (n.y > n.x && n.y > n.z)
			{
				uvs[triangles[i + 0]] = new Vector2(a.x, a.z);
				uvs[triangles[i + 1]] = new Vector2(b.x, b.z);
				uvs[triangles[i + 2]] = new Vector2(c.x, c.z);
			}
			else if (n.z > n.x && n.z > n.y)
			{
				uvs[triangles[i + 0]] = new Vector2(a.x, a.y);
				uvs[triangles[i + 1]] = new Vector2(b.x, b.y);
				uvs[triangles[i + 2]] = new Vector2(c.x, c.y);
			}
		}
		return uvs;
	}

	public void Export()
	{
		if (_TriangleBuffer == null || _IndirectBuffer == null) return;
		int[] data = new int[4];
		_IndirectBuffer.GetData(data);
		int count = data[0];
		Mesh mesh = new Mesh();
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		Point[] points = new Point[count];
		_TriangleBuffer.GetData(points);
		List<Vector3> vertices = new List<Vector3>();
		List<int> triangles = new List<int>();
		List<Vector3> normals = new List<Vector3>();
		List<Color> colors = new List<Color>();
		for (int i = 0; i < points.Length; i++)
		{
			vertices.Add(points[i].position * Scale);
			triangles.Add(i);
			normals.Add(points[i].normal);
			colors.Add(new Color(points[i].color.x, points[i].color.y, points[i].color.z));
		}
		mesh.vertices = vertices.ToArray();
		mesh.triangles = triangles.ToArray();
		mesh.normals = normals.ToArray();
		mesh.colors = colors.ToArray();
		if (GeneratePerTriangleUV) mesh.uv = CubeProjection(vertices, triangles);
		CheckPath("Meshes");
		string fileName = _Hash;
		AssetDatabase.CreateAsset(mesh, "Assets/Meshes/" + fileName + ".asset");
		GameObject target = new GameObject();
		target.name = fileName;
		target.AddComponent<MeshFilter>().sharedMesh = mesh;
		Material material = new Material(SurfaceShader);
		material.SetTexture("_MainTex", SurfaceTexture);
		CheckPath("Materials");
		AssetDatabase.CreateAsset(material, "Assets/Materials/" + fileName + ".mat");
		MeshRenderer renderer = target.AddComponent<MeshRenderer>();
		renderer.sharedMaterial = material;
		CheckPath("Prefabs");
		PrefabUtility.SaveAsPrefabAsset(target, "Assets/Prefabs/" + fileName + ".prefab");
	}

	void OnRenderObject()
	{
		if (Resolution != _CurrentResolution)
		{
			_VolumeTexture.Release();
			GenerateVolumeTexture(Resolution);
			_CurrentResolution = Resolution;
		}
		ScalarFieldCS.SetInt("_Resolution", Resolution);
		ScalarFieldCS.SetInt("_Steps", Steps);
		ScalarFieldCS.SetFloat("_Smoothness", Smoothness);
		ScalarFieldCS.SetFloat("_Seed", Seed);
		ScalarFieldCS.SetFloat("_DisplacementScale", DisplacementScale);
		ScalarFieldCS.SetFloat("_DisplacementSpread", DisplacementSpread);
		ScalarFieldCS.SetTexture(0, "_VolumeTexture", _VolumeTexture);
		ScalarFieldCS.Dispatch(0, Resolution / 8, Resolution / 8, Resolution / 8);
		TriangulationCS.SetInt("_Resolution", Resolution);
		TriangulationCS.SetTexture(0, "_VolumeTexture", _VolumeTexture);
		TriangulationCS.SetBuffer(0, "_TriangleBuffer", _TriangleBuffer);
		TriangulationCS.SetBuffer(0, "_CounterBuffer", _CounterBuffer);
		_TriangleBuffer.SetCounterValue(0);
		_CounterBuffer.SetCounterValue(0);
		TriangulationCS.Dispatch(0, Resolution / 8, Resolution / 8, Resolution / 8);
		int[] args = new int[] { 0, 1, 0, 0 };
		_IndirectBuffer.SetData(args);
		ComputeBuffer.CopyCount(_CounterBuffer, _IndirectBuffer, 0);
		_Material.SetFloat("_Scale", Scale);
		_Material.SetBuffer("_TriangleBuffer", _TriangleBuffer);
		_Material.SetInt("_ShowNormals", System.Convert.ToInt32(ShowNormals));
		_Material.SetInt("_Wireframe", System.Convert.ToInt32(Wireframe));
		_Material.SetPass(0);
		Graphics.DrawProceduralIndirectNow(MeshTopology.Triangles, _IndirectBuffer);
	}

	void OnDestroy()
	{
		if (_TriangleBuffer != null) _TriangleBuffer.Release();
		if (_IndirectBuffer != null) _IndirectBuffer.Release();
		if (_CounterBuffer != null) _CounterBuffer.Release();
		if (_VolumeTexture != null) _VolumeTexture.Release();
		if (_Material != null) Destroy(_Material);
	}
}