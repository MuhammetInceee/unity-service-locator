#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Attributes
{
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]

    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false; // ReadOnly
            EditorGUI.PropertyField(position, property, label);
            GUI.enabled = true; // Restore GUI enabled state
        }
    }
}
#endif