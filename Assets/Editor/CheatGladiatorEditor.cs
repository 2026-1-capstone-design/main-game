using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CheatGladiator))]
public class CheatGladiatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CheatGladiator cheatScript = (CheatGladiator)target;

        GUILayout.Space(10);

        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        if (GUILayout.Button("검투사 인벤토리에 추가", GUILayout.Height(30)))
        {
            cheatScript.GiveCheatGladiator();
        }
    }
}
