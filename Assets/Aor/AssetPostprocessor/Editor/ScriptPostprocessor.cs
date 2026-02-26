using System.IO;
using System.Text;
using UnityEditor;

public class ScriptPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        ScriptUtf8(importedAssets);
    }

    static void ScriptUtf8(string[] importedAssets)
    {
        foreach (string path in importedAssets)
        {
            if (path.EndsWith(".cs"))
            {
                string fullPath = Path.GetFullPath(path);
                string content = File.ReadAllText(fullPath);

                byte[] bytes = File.ReadAllBytes(fullPath);
                bool hasBOM = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

                if (!hasBOM)
                {
                    File.WriteAllText(fullPath, content, Encoding.UTF8);
                }
            }
        }
    }
}