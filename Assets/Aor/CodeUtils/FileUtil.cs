using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Aor.Persistence;
using UnityEngine;

public class FileUtil
{
    static string persistentDataPath => Application.persistentDataPath;
    static string dataPath;
    static RuntimePlatform platform;
    static string userID;
    public static void Init(string _userID)
    {
        dataPath = Application.dataPath;
        platform = Application.platform;
        if (string.IsNullOrEmpty(_userID))
        {
            _userID = "UnnamedUser";
        }
        userID = _userID;
    }
    public static string GetUserLocalStoragePath()
    {
        return _GetStoragePath("storage_local");
    }
    static string _GetStoragePath(string postfix)
    {
        string result = $"{persistentDataPath}/{userID}/{postfix}/";
        if (Directory.Exists(result) == false)
        {
            Directory.CreateDirectory(result);
        }
        return result;
    }
    // E:/folder1/folder2/filename.txt => filename
    public static string GetFileNameWithoutExtension(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        // 处理可能存在的目录分隔符差异
        path = path.Replace('\\', '/');

        // 查找最后一个斜杠的位置
        int lastSlashIndex = path.LastIndexOf('/');

        // 提取文件名部分
        string fileName = lastSlashIndex >= 0
            ? path.Substring(lastSlashIndex + 1)
            : path;

        // 查找扩展名的点位置
        int dotIndex = fileName.LastIndexOf('.');

        // 返回不包含扩展名的文件名
        return dotIndex >= 0
            ? fileName.Substring(0, dotIndex)
            : fileName;
    }
    public static string GetRootStoragePath()
    {
        return $"{persistentDataPath}/";
    }
    public static string GetUserStoragePath()
    {
        return _GetStoragePath("storage");
    }
    public static string GetUnityStoragePath()
    {
        string path = "";
        if (platform == RuntimePlatform.Android)
        {
            path = persistentDataPath;
        } else if (platform == RuntimePlatform.WindowsPlayer)
        {
            path = dataPath;
        } else if (platform == RuntimePlatform.WindowsEditor)
        {
            path = dataPath;
            if (path.EndsWith("Assets"))
            {
                path += "/StreamingAssets/Storage";
            }
        }
        Debug.Log("storage path:" + path);
        return path;
    }
    public static void Test()
    {
        string path = GetUserStoragePath();
        string content = LoadFile(path, "test.txt");
        if (content == "error")
        {
            CreateOrWriteTextFile(path, "test.txt", "TestContent");
            content = LoadFile(path, "test.txt");
        }
        Debug.Log(content);
    }

    #region STRING
    public static void CreateOrWriteTextFile(string path, string name, string info)
    {
        StreamWriter sw;
        FileInfo t = new FileInfo(path + "//" + name);
        if (!t.Exists)
        {
            sw = t.CreateText();
        } else
        {
            sw = t.AppendText();
        }
        sw.WriteLine(info);
        sw.Close();
        sw.Dispose();
    }
    public static void DeleteFileInStorage(string name)
    {
        var path = GetUserStoragePath() + "//" + name;
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
    public static void DeleteFile(string name)
    {
        File.Delete(name);
    }
    #endregion
    #region BINARY
    // directly_write=False的时候，会先写到临时文件中，写成功了再copy覆盖目标文件。
    public static bool Serialize(string filename, object obj, bool directly_write)
    {
        if (filename == "" || filename == null)
        {
            Debug.LogError("filname cannot be empty");
            return false;
        }
        string temp_filename = filename;
        if (!directly_write)
        {
            temp_filename += ".writing";
        }
        Stream file = null;
        var temp_full_path = temp_filename;
        try
        {
            if (filename.EndsWith(".json"))
            {
                var json = JsonDataHelper.instance;
                json.SerializeToFile(temp_full_path, obj);
            } else
            {
                file = new FileStream(temp_full_path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                IFormatter formatter = new BinaryFormatter();
                formatter.Serialize(file, obj);
            }
        } catch (Exception e)
        {
            Debug.LogError(e);
            return false;
        } finally
        {
            if (file != null)
            {
                file.Close();
            }
        }
        try
        {
            if (!directly_write)
            {
                File.Copy(temp_full_path, filename, true);
                File.Delete(temp_full_path);
            }
            return true;
        } catch (Exception e)
        {
            Debug.LogError("failed to copy file:" + e.ToString());
            return false;
        }
    }
    // source: https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
    public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }
    public static object DeserializeJson(string filename, Type data_type)
    {
        var json = JsonDataHelper.instance;
        return json.DeserializeFromFile(filename, data_type);
    }
    // in_storage=true 表示在 external_assets/storage
    // =false 表示在 external_assets/lua/mods
    public static object Deserialize(string filename, Type data_type)
    {
        if (filename.EndsWith(".json"))
        {
            return DeserializeJson(filename, data_type);
        }

        // 下面对二进制文件进行反序列化
        Stream file = null;
        try
        {
            file = new FileStream(filename, FileMode.Open);
        } catch (Exception)
        {
            if (file != null)
            {
                file.Close();
            }
            return null;
        }
        try
        {
            IFormatter formatter = new BinaryFormatter();
            object obj = formatter.Deserialize(file);
            return obj;
        } catch (Exception e)
        {
            Debug.LogError(e);
            return null;
        } finally
        {
            file.Close();
        }
    }
    #endregion
    #region COMMON
    public static void DeleteFile(string path, string name)
    {
        File.Delete(path + "//" + name);
    }
    public static string LoadFile(string path, string name)
    {
        return LoadFile(Path.Combine(path, name));
    }
    public static bool FileExists(string filename)
    {
        FileInfo t = new FileInfo(filename);
        return t.Exists;
    }
    public static string LoadFile(string filename)
    {
        FileInfo t = new FileInfo(filename);
        if (!t.Exists)
        {
            Debug.LogError("file not exists: " + filename);
            return "error";
        }
        StreamReader sr = null;
        sr = File.OpenText(filename);
        var content = sr.ReadToEnd();
        sr.Close();
        sr.Dispose();
        return content;
    }
    public static bool SaveFile(string filename, string content)
    {
        filename = GetUserStoragePath() + filename;
        if (System.IO.File.Exists(filename))
        {
            using (StreamWriter sw = new StreamWriter(filename, false))
            {
                sw.Write(content);
            }
        } else
        {
            FileStream fs1 = new FileStream(filename, FileMode.Create, FileAccess.Write);//创建写入文件 
            StreamWriter sw = new StreamWriter(fs1);
            sw.Write(content);//开始写入值
            sw.Close();
            sw.Dispose();
            fs1.Close();
            fs1.Dispose();
        }
        return true;
    }
    public static Texture2D LoadTextureWithWWW(string filename)
    {
#pragma warning disable CS0618 // 类型或成员已过时
        return new WWW("file://" + filename).texture;
#pragma warning restore CS0618 // 类型或成员已过时
    }
    public static Texture2D LoadTexture(string filename)
    {
        //创建文件读取流
        FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
        fileStream.Seek(0, SeekOrigin.Begin);
        //创建文件长度缓冲区
        byte[] bytes = new byte[fileStream.Length];
        //读取文件
        fileStream.Read(bytes, 0, (int)fileStream.Length);
        //释放文件读取流
        fileStream.Close();
        fileStream.Dispose();
        fileStream = null;

        //创建Texture
        var tex_format = TextureFormat.RGBA32;
        if (!filename.EndsWith(".png"))
        {
            tex_format = TextureFormat.RGB24;
        }
        Texture2D texture = new Texture2D(1, 1, tex_format, true); // 后面texture.LoadImage会自动调整width和height
        texture.LoadImage(bytes, true);
        return texture;
    }
    #endregion
}
