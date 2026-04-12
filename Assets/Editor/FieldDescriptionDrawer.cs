using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FieldDescriptionAttribute))]
public sealed class FieldDescriptionDrawer : PropertyDrawer
{
    private const float Padding = 2f;
    private static readonly GUIStyle DescriptionStyle = new GUIStyle(EditorStyles.helpBox)
    {
        fontSize = 14,
        wordWrap = true,
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var attr = (FieldDescriptionAttribute)attribute;
        float fieldHeight = EditorGUI.GetPropertyHeight(property, label, true);
        float textHeight = DescriptionStyle.CalcHeight(
            new GUIContent(attr.Text),
            EditorGUIUtility.currentViewWidth - 20f
        );
        return fieldHeight + Padding + textHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = (FieldDescriptionAttribute)attribute;

        float fieldHeight = EditorGUI.GetPropertyHeight(property, label, true);
        Rect fieldRect = new Rect(position.x, position.y, position.width, fieldHeight);
        EditorGUI.PropertyField(fieldRect, property, label, true);

        float textHeight = position.height - fieldHeight - Padding;
        Rect descRect = new Rect(position.x, position.y + fieldHeight + Padding, position.width, textHeight);

        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.6f);
        GUI.Label(descRect, attr.Text, DescriptionStyle);
        GUI.color = prev;
    }
}
