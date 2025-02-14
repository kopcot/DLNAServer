using DLNAServer.Helpers.Serializations.Converters;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DLNAServer.Helpers.Serializations
{
    internal static class JsonSerialization
    {
        private static readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            WriteIndented = true,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(),
                new DlnaMimeKeyValuePairConverter()
            }
        };
        public static T LoadFromJsonOrCreateNew<T>(string fileFullPath, Func<T> createNew)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(fileFullPath);
            ArgumentNullException.ThrowIfNull(createNew);

            FileInfo fileInfo = new(fileFullPath);
            if (fileInfo.Exists)
            {
                try
                {
                    using (var fileStream = new FileStream(fileFullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var reader = new StreamReader(fileStream))
                    {
                        string fileContent = reader.ReadToEnd();
                        var configLoaded = JsonSerializer.Deserialize<T>(fileContent, jsonSerializerOptions);
                        if (configLoaded != null)
                        {
                            return configLoaded;
                        }
                    };
                }
                catch
                {
                    string backupFileName = $"{fileFullPath}.{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                    fileInfo.MoveTo(backupFileName, overwrite: true);
                }
            }
            var config = createNew();
            return SaveToJson(fileFullPath, config, true, createNew);
        }

        public static T SaveToJson<T>(string fileFullPath, T config, bool saveNewConfigOnError, Func<T> createNew)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(fileFullPath);
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(createNew);

            try
            {
                FileInfo fileInfo = new(fileFullPath);
                if (!fileInfo.Directory!.Exists)
                {
                    fileInfo.Directory.Create();
                    if (OperatingSystem.IsLinux())
                    {
                        fileInfo.Directory.UnixFileMode =
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
                    }
                }
                using (FileStream fileStream = new(fileFullPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    JsonSerializer.Serialize(fileStream, config, jsonSerializerOptions);
                }
                return config;
            }
            catch
            {
                var configNew = createNew()!;
                if (saveNewConfigOnError)
                {
                    _ = SaveToJson(fileFullPath, configNew, false, createNew);
                }
                return configNew;
            }
        }
    }
}