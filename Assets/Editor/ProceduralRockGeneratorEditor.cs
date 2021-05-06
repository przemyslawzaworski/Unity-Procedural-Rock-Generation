using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ProceduralRockGenerator))]
public class ProceduralRockGeneratorEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();    
		ProceduralRockGenerator prg = (ProceduralRockGenerator)target;
		if(GUILayout.Button("Export")) prg.Export();
	}
}
