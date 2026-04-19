using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class ScriptPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        List<string> modifiedFiles = new List<string>();

        foreach (string path in importedAssets)
        {
            if (path.EndsWith(".cs") && TryAddUtf8Bom(path))
            {
                modifiedFiles.Add(path);
            }
        }

        // 所有文件处理完后，只刷新一次
        if (modifiedFiles.Count > 0)
        {
            string fileList = string.Join("\n  ", modifiedFiles);
            Debug.Log($"已为 {modifiedFiles.Count} 个文件添加 UTF-8 BOM：\n  {fileList}");
            AssetDatabase.Refresh();
        }
    }

    static bool TryAddUtf8Bom(string assetPath)
    {
        string fullPath = Path.GetFullPath(assetPath);
        byte[] bytes = File.ReadAllBytes(fullPath);

        // 检查是否已有 BOM
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return false;

        // 添加 BOM
        string content = Encoding.UTF8.GetString(bytes);
        File.WriteAllText(fullPath, content, new UTF8Encoding(true));
        return true;
    }
}