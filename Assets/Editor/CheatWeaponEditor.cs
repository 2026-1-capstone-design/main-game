using UnityEngine;
using UnityEditor;

// CheatWeapon 스크립트의 인스펙터 UI를 커스텀하겠다고 선언합니다.
[CustomEditor(typeof(CheatWeapon))]
public class CheatWeaponEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 1. 기존 변수들(WeaponType, SkillId 등)을 기본적으로 다 그려줍니다.
        DrawDefaultInspector();

        // 현재 선택된 CheatWeapon 스크립트를 가져옵니다.
        CheatWeapon cheatScript = (CheatWeapon)target;

        // 2. 버튼 위에 살짝 여백을 줍니다.
        GUILayout.Space(10);


        // 3. 인스펙터에 버튼을 그립니다. (세로 높이 30짜리 큼직한 버튼)
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        if (GUILayout.Button("무기 인벤토리에 추가", GUILayout.Height(30)))
        {
            // 버튼을 클릭하면 CheatWeapon 스크립트의 함수가 실행됩니다!
            cheatScript.GiveCheatWeapon();
        }
    }
}
