using System.Text.Json;
using System.Text.Json.Serialization;

namespace Homer.NetDaemon.Services.DataMall.BusArrival;

/// <summary>Reads an empty or malformed date string as null instead of failing the whole response.</summary>
public sealed class EmptyStringAsNullDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.TryGetDateTime(out var value))
        {
            return value;
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is { } dateTime)
        {
            writer.WriteStringValue(dateTime);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
