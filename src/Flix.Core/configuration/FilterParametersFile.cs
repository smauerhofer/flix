using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Flix.Core;

public static class FilterParametersFile
{
  private static readonly ISerializer Serializer = new SerializerBuilder()
    .WithNamingConvention(PascalCaseNamingConvention.Instance)
    .Build();

  private static readonly IDeserializer Deserializer = new DeserializerBuilder()
    .WithNamingConvention(PascalCaseNamingConvention.Instance)
    .Build();

  public static FilterParameters Read(string path)
  {
    string yaml = File.ReadAllText(path);
    FilterParameters result = Deserializer.Deserialize<FilterParameters>(yaml)
      ?? throw new InvalidDataException("Empty filter configuration.");
    result.Validate();
    return result;
  }

  public static void Write(string path, FilterParameters parameters)
  {
    ArgumentNullException.ThrowIfNull(parameters);
    parameters.Validate();
    string fullPath = Path.GetFullPath(path);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try
    {
      File.WriteAllText(temporaryPath, Serializer.Serialize(parameters));
      File.Move(temporaryPath, fullPath, true);
    }
    finally
    {
      if (File.Exists(temporaryPath))
      {
        File.Delete(temporaryPath);
      }
    }
  }
}
