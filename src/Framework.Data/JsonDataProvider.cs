using Newtonsoft.Json;

namespace Framework.Data;

/// <summary>
/// Reads test data from JSON files and deserialises them into strongly-typed models.
/// Use <see cref="Read{T}"/> to load any JSON file by path; throws
/// <see cref="FileNotFoundException"/> if the file does not exist.
/// </summary>
public static class JsonDataProvider
{
    public static T? Read<T>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"JSON file not found: {filePath}", filePath);
        }

        var content = File.ReadAllText(filePath);
        return JsonConvert.DeserializeObject<T>(content);
    }
}
