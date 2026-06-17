using System.Text;
using System.Text.Json;
using Saku_Overclock.Core.Contracts.Services;

namespace Saku_Overclock.Core.Services;

public class FileService : IFileService
{
    // Выносим общие настройки, чтобы везде применялись одинаково
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        IncludeFields = true
    };

    public T? Read<T>(string folderPath, string fileName)
    {
        var path = Path.Combine(folderPath, fileName);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, _options);
        }

        return default;
    }

    public void Save<T>(string folderPath, string fileName, T content)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var fileContent = JsonSerializer.Serialize(content, _options); 
        try
        {
            File.WriteAllText(Path.Combine(folderPath, fileName), fileContent, Encoding.UTF8);
        }
        catch (Exception e)
        {
            // Невозможно сохранить файл
        }
    }
}