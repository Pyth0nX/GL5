#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EventPayload))]
public class EventPayloadDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty eventNameProp = property.FindPropertyRelative("_eventName");
        SerializedProperty dataTypeProp = property.FindPropertyRelative("_dataType");

        EventDataType currentType = (EventDataType)dataTypeProp.enumValueIndex;

        // Horizontal distribution
        float spacing = 5f;
        float totalWidth = position.width;
        float y = position.y + 2f;
        float height = EditorGUIUtility.singleLineHeight;

        if (currentType == EventDataType.NONE)
        {
            // If it doesn't require a value, 65% name and 35% type
            float nameWidth = (totalWidth - spacing) * 0.65f;
            float typeWidth = (totalWidth - spacing) * 0.35f;

            Rect rName = new Rect(position.x, y, nameWidth, height);
            Rect rType = new Rect(rName.xMax + spacing, y, typeWidth, height);

            EditorGUI.PropertyField(rName, eventNameProp, GUIContent.none);
            EditorGUI.PropertyField(rType, dataTypeProp, GUIContent.none);
        }
        else
        {
            // If it requires a value, 45% name, 25% type, 30% value
            float nameWidth = (totalWidth - (spacing * 2)) * 0.45f;
            float typeWidth = (totalWidth - (spacing * 2)) * 0.25f;
            float valWidth = (totalWidth - (spacing * 2)) * 0.30f;

            Rect rName = new Rect(position.x, y, nameWidth, height);
            Rect rType = new Rect(rName.xMax + spacing, y, typeWidth, height);
            Rect rVal = new Rect(rType.xMax + spacing, y, valWidth, height);

            EditorGUI.PropertyField(rName, eventNameProp, GUIContent.none);
            EditorGUI.PropertyField(rType, dataTypeProp, GUIContent.none);

            switch (currentType)
            {
                case EventDataType.STRING:
                    EditorGUI.PropertyField(rVal, property.FindPropertyRelative("_stringValue"), GUIContent.none);
                    break;
                case EventDataType.INT:
                    EditorGUI.PropertyField(rVal, property.FindPropertyRelative("_intValue"), GUIContent.none);
                    break;
                case EventDataType.FLOAT:
                    EditorGUI.PropertyField(rVal, property.FindPropertyRelative("_floatValue"), GUIContent.none);
                    break;
                case EventDataType.BOOL:
                    EditorGUI.PropertyField(rVal, property.FindPropertyRelative("_boolValue"), GUIContent.none);
                    break;
            }
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight + 4f;
    }
}
#endif