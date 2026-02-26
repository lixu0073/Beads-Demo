using UnityEditor;
using UnityEngine;

namespace Aor.UI
{
    [CustomEditor(typeof(SafeAreaUGUI))]
    [CanEditMultipleObjects] // 支持多选编辑
    public class SafeAreaEditor : Editor
    {
        // 定义序列化属性，利用 SerializedProperty 处理撤销/重做
        SerializedProperty adjustTop, adjustBottom, adjustLeft, adjustRight;
        SerializedProperty topPadding, bottomPadding, leftPadding, rightPadding;

        private void OnEnable()
        {
            adjustTop = serializedObject.FindProperty("adjustTop");
            adjustBottom = serializedObject.FindProperty("adjustBottom");
            adjustLeft = serializedObject.FindProperty("adjustLeft");
            adjustRight = serializedObject.FindProperty("adjustRight");

            topPadding = serializedObject.FindProperty("topPadding");
            bottomPadding = serializedObject.FindProperty("bottomPadding");
            leftPadding = serializedObject.FindProperty("leftPadding");
            rightPadding = serializedObject.FindProperty("rightPadding");
        }

        public override void OnInspectorGUI()
        {
            // 更新序列化对象状态
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("安全区适配配置", EditorStyles.boldLabel);

            // 绘制顶部配置
            DrawToggleArea("Top", adjustTop, topPadding);
            // 绘制底部配置
            DrawToggleArea("Bottom", adjustBottom, bottomPadding);
            // 绘制左侧配置
            DrawToggleArea("Left", adjustLeft, leftPadding);
            // 绘制右侧配置
            DrawToggleArea("Right", adjustRight, rightPadding);

            // 应用修改（支持 Undo）
            serializedObject.ApplyModifiedProperties();

            // 如果在编辑器下修改了值，手动触发一次刷新预览
            if (GUI.changed)
            {
                (target as SafeAreaUGUI)?.ApplySafeArea();
            }
        }

        private void DrawToggleArea(string label, SerializedProperty toggle, SerializedProperty padding)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox); // 使用 HelpBox 包裹使区域更明显

            // 1. 绘制开关行
            EditorGUILayout.PropertyField(toggle, new GUIContent(label));

            // 2. 如果开关打开，在下一行绘制缩进的滑动条
            if (toggle.boolValue)
            {
                // 增加缩进级别
                EditorGUI.indentLevel++;

                // 绘制滑动条，这里给一个简短的标签描述
                EditorGUILayout.PropertyField(padding, new GUIContent("额外偏移量"));

                // 恢复缩进级别
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(1);
        }
    }
}