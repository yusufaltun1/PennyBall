using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(InspectorLabelAttribute))]
public sealed class InspectorLabelDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = (InspectorLabelAttribute)attribute;
        if (!string.IsNullOrEmpty(attr.Label))
        {
            label.text = attr.Label;
        }

        EditorGUI.PropertyField(position, property, label, includeChildren: true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
    }
}
