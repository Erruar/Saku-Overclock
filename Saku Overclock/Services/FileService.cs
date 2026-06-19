using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;

namespace Saku_Overclock.Services;

[JsonSourceGenerationOptions(WriteIndented = true, IncludeFields = true)]
[JsonSerializable(typeof(List<Notify>))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(LocalThemeSettingsOptions))]
[JsonSerializable(typeof(List<NiIconsElements>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Preset[]))]
[JsonSerializable(typeof(RtssSettings))]
internal partial class SakuJsonContext : JsonSerializerContext
{
}

public class FileService : IFileService
{
    public T? Read<T>(string folderPath, string fileName)
    {
        var path = Path.Combine(folderPath, fileName);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);

            if (SakuJsonContext.Default.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> jsonTypeInfo)
            {
                return JsonSerializer.Deserialize(json, jsonTypeInfo);
            }

            throw new NotImplementedException();
        }

        return default;
    }

    public void Save<T>(string folderPath, string fileName, T content)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileContent;

        if (SakuJsonContext.Default.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> jsonTypeInfo)
        {
            fileContent = JsonSerializer.Serialize(content, jsonTypeInfo);
        }
        else
        {
            throw new NotImplementedException();
        }

        try
        {
            File.WriteAllText(Path.Combine(folderPath, fileName), fileContent, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }
    }
}