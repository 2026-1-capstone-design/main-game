using UnityEngine;

public sealed class FieldDescriptionAttribute : PropertyAttribute
{
    public readonly string Text;

    public FieldDescriptionAttribute(string text)
    {
        Text = text;
    }
}
