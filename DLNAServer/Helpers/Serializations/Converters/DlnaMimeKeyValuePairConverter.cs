using DLNAServer.Types.DLNA;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace DLNAServer.Helpers.Serializations.Converters
{
    public class DlnaMimeKeyValuePairConverter : JsonConverter<KeyValuePair<DlnaMime, string?>>
    {
        public override KeyValuePair<DlnaMime, string?> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Read the JSON object into a dictionary entry
            JsonDocument doc = JsonDocument.ParseValue(ref reader);
            var obj = doc.RootElement.EnumerateObject();

            foreach (var property in obj)
            {
                if (Enum.TryParse<DlnaMime>(property.Name, out DlnaMime key))
                {
                    var value = property.Value.GetString(); // Get the value as string
                    return new KeyValuePair<DlnaMime, string?>(key, value);  // Return as KeyValuePair
                }
            }

            throw new JsonException("Invalid format for KeyValuePair<DlnaMime, string?>");
        }

        public override void Write(Utf8JsonWriter writer, KeyValuePair<DlnaMime, string?> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(
                propertyName: string.Format("{0}", [value.Key]),
                value: value.Value
                );
            writer.WriteEndObject();
        }
    }
}
