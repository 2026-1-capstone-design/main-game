using UnityEngine;
using UnityEditor;

// CheatArtifact 스크립트의 인스펙터 UI에 버튼을 추가합니다.
[CustomEditor(typeof(CheatArtifact))]
public class CheatArtifactEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 1. 기존 변수(InventoryManager, ArtifactSO 등)들을 그립니다.
        DrawDefaultInspector();

        CheatArtifact cheatScript = (CheatArtifact)target;

        // 2. 여백 추가
        GUILayout.Space(10);

        // 3. 큼직한 실행 버튼 추가
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        if (GUILayout.Button("장신구 인벤토리에 추가", GUILayout.Height(30)))
        {
            // 버튼 클릭 시 치트 함수 실행
            cheatScript.GiveCheatArtifact();
        }
    }
}
