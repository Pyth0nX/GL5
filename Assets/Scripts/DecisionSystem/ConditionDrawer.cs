using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom PropertyDrawer for the Condition struct.
/// Dynamically displays only the relevant fields based on the selected ConditionTargetType
/// and ComparisonOperator (e.g. Min/Max range for 'Between' or Tier dropdown).
/// </summary>
[CustomPropertyDrawer(typeof(Condition))]
public class ConditionDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2f;
    private const float HorizontalSpacing = 4f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty targetTypeProp = property.FindPropertyRelative("targetType");
        SerializedProperty decisionStatProp = property.FindPropertyRelative("decisionStat");
        SerializedProperty varNameProp = decisionStatProp.FindPropertyRelative("name");
        SerializedProperty opProp = property.FindPropertyRelative("comparisonOperator");
        SerializedProperty valProp = property.FindPropertyRelative("value");
        SerializedProperty maxValProp = property.FindPropertyRelative("maxValue");
        SerializedProperty tierProp = property.FindPropertyRelative("targetTier");

        ConditionTargetType targetType = (ConditionTargetType)targetTypeProp.enumValueIndex;
        ComparisonOperator op = (ComparisonOperator)opProp.enumValueIndex;

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float currentY = position.y + VerticalSpacing;

        // Line 1: Target condition type
        Rect line1Rect = new Rect(position.x, currentY, position.width, lineHeight);
        EditorGUI.PropertyField(line1Rect, targetTypeProp, new GUIContent("Evaluate", "Metric or state evaluated by this condition"));
        currentY += lineHeight + VerticalSpacing;

        // Optional line: Variable name if evaluating a DecisionVariable
        if (targetType == ConditionTargetType.DecisionVariable)
        {
            Rect varRect = new Rect(position.x, currentY, position.width, lineHeight);
            EditorGUI.PropertyField(varRect, varNameProp, new GUIContent("Variable Name", "Name of the integer variable in DecisionDataManager"));
            currentY += lineHeight + VerticalSpacing;
        }

        // Last line: Operator + Value / Range / Tier
        float opWidth = (position.width - HorizontalSpacing) * 0.45f;
        float rightWidth = position.width - HorizontalSpacing - opWidth;

        Rect opRect = new Rect(position.x, currentY, opWidth, lineHeight);
        Rect rightRect = new Rect(opRect.xMax + HorizontalSpacing, currentY, rightWidth, lineHeight);

        EditorGUI.PropertyField(opRect, opProp, GUIContent.none);

        int prevIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        if (targetType == ConditionTargetType.ReputationTier && op != ComparisonOperator.Between)
        {
            EditorGUI.PropertyField(rightRect, tierProp, GUIContent.none);
        }
        else if (op == ComparisonOperator.Between)
        {
            float halfWidth = (rightWidth - HorizontalSpacing) * 0.5f;
            Rect minRect = new Rect(rightRect.x, currentY, halfWidth, lineHeight);
            Rect maxRect = new Rect(minRect.xMax + HorizontalSpacing, currentY, halfWidth, lineHeight);

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 28f;
            EditorGUI.PropertyField(minRect, valProp, new GUIContent("Min"));
            EditorGUI.PropertyField(maxRect, maxValProp, new GUIContent("Max"));
            EditorGUIUtility.labelWidth = prevLabelWidth;
        }
        else
        {
            EditorGUI.PropertyField(rightRect, valProp, GUIContent.none);
        }

        EditorGUI.indentLevel = prevIndent;

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty targetTypeProp = property.FindPropertyRelative("targetType");
        ConditionTargetType targetType = (ConditionTargetType)targetTypeProp.enumValueIndex;

        int lines = targetType == ConditionTargetType.DecisionVariable ? 3 : 2;
        return (EditorGUIUtility.singleLineHeight * lines) + (VerticalSpacing * (lines + 1)) + 2f;
    }
}
