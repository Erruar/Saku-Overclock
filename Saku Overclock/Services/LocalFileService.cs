using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Shared;

namespace Saku_Overclock.Services;

public class LocalFileService : IFileService
{
    public T? Read<T>(string folderPath, string fileName)
    {
        var path = Path.Combine(folderPath, fileName);
        if (!File.Exists(path)) return default;
        var json = File.ReadAllText(path);
        if (IpcJsonContext.Default.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> ctx)
            return JsonSerializer.Deserialize(json, ctx);
        return default;
    }

    public void Save<T>(string folderPath, string fileName, T content)
    {
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
        if (IpcJsonContext.Default.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> typeInfo)
            File.WriteAllText(Path.Combine(folderPath, fileName), JsonSerializer.Serialize(content, typeInfo), Encoding.UTF8);
    }
}