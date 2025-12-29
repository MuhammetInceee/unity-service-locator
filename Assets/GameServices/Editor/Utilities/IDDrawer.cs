using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;


namespace Attributes
{
    [CustomPropertyDrawer(typeof(IDAttribute))]
    public class IDDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var idRect = new Rect(position.x, position.y, position.width - 100, position.height);
            EditorGUI.PropertyField(idRect, property, label);

            var buttonRect = new Rect(position.x + position.width - 100, position.y, 100, position.height);
            if (GUI.Button(buttonRect, "Generate"))
            {
                if (EditorUtility.DisplayDialog("Generate New ID",
                        "Are you sure you want to generate a new ID?",
                        "Yes", "No"))
                {
                    property.stringValue = Guid.NewGuid().ToString();
                    property.serializedObject.ApplyModifiedProperties();
                }
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif