using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class ScriptEditorTools : EditorWindow
{
    [MenuItem("Tools/脚本工具/转换脚本编码 (UTF-8 BOM)", false, 100)]
    public static void ConvertAllScriptsToUtf8BOM()
    {
        // 获取项目中所有的 .cs 文件路径
        string[] allScriptPaths = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

        int changedCount = 0;
        float progress = 0;

        try
        {
            for (int i = 0; i < allScriptPaths.Length; i++)
            {
                string filePath = allScriptPaths[i];

                // 显示进度条
                progress = (float)i / allScriptPaths.Length;
                if (EditorUtility.DisplayCancelableProgressBar("编码转换中", $"正在处理: {Path.GetFileName(filePath)}", progress))
                {
                    Debug.LogWarning("用户取消了编码转换操作");
                    break;
                }

                if (EnsureUtf8BOM(filePath))
                {
                    changedCount++;
                }
            }
        } finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            Debug.Log($"<color=green><b>编码转换完成！</b></color> 共处理 {allScriptPaths.Length} 个文件，修改了 {changedCount} 个非标准编码文件。");
        }
    }

    /// <summary>
    /// 检查并转换为 UTF-8 BOM
    /// </summary>
    private static bool EnsureUtf8BOM(string filePath)
    {
        byte[] fileBytes = File.ReadAllBytes(filePath);

        // UTF-8 BOM 的字节序列为: 0xEF, 0xBB, 0xBF
        if (fileBytes.Length >= 3 && fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF)
        {
            return false;
        }

        string content = File.ReadAllText(filePath);

        // 使用带 BOM 的 UTF8 重新写入
        // Encoding.UTF8 在 .NET 中默认就是带 BOM 的
        File.WriteAllText(filePath, content, Encoding.UTF8);

        return true;
    }
}
