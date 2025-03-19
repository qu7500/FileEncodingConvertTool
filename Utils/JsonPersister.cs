using System;
using System.IO;
using System.Text.Json;
namespace FileEncodingChecker.Utils;

public static class JsonPersister
{
    public static void Save<T>(string path, T data)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            if (!File.Exists(path))
            {
                File.Create(path).Close();
            }
            var json = JsonSerializer.Serialize(data);
            File.WriteAllText(path, json);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public static T? Load<T>(string path) where T : new()
    {
        try
        {
            if (!File.Exists(path)) return new T();
            var data =  File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(data);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return new T();
        }
    }
}
