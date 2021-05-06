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

	Material _Material;
	RenderTexture _VolumeTexture;
	ComputeBuffer _TriangleBuffer;
	ComputeBuffer _IndirectBuffer;
	int _Count, _CurrentResolution;
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
		TriangulationCS.SetFloat("_Threshold", 0.5f);
	}

	void CheckPath (string directory)
	{
		if(!Directory.Exists(Path.Combine(Application.dataPath, directory)))
		{
			Directory.CreateDirectory(Path.Combine(Application.dataPath, directory));
		}
	}

	public void Export()
	{
		Mesh mesh = new Mesh();
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		Point[] points = new Point[_Count];
		if (_TriangleBuffer == null) return;
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
		_TriangleBuffer.SetCounterValue(0);
		TriangulationCS.Dispatch(0, Resolution / 8, Resolution / 8, Resolution / 8);
		_Material.SetFloat("_Scale", Scale);
		_Material.SetBuffer("_TriangleBuffer", _TriangleBuffer);
		_Material.SetInt("_ShowNormals", System.Convert.ToInt32(ShowNormals));
		_Material.SetInt("_Wireframe", System.Convert.ToInt32(Wireframe));
		int[] args = new int[] { 0, 1, 0, 0 };
		_IndirectBuffer.SetData(args);
		ComputeBuffer.CopyCount(_TriangleBuffer, _IndirectBuffer, 0);
		_IndirectBuffer.GetData(args);
		args[0] *= 3;
		_IndirectBuffer.SetData(args);
		_Count = args[0];
		_Material.SetPass(0);
		Graphics.DrawProceduralIndirectNow(MeshTopology.Triangles, _IndirectBuffer);
	}

	void OnDestroy()
	{
		_TriangleBuffer.Release();
		_IndirectBuffer.Release();
		_VolumeTexture.Release();
		Destroy(_Material);
	}
}