using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseGig.Api.Common.Json;

internal sealed class DateOnlyDdMmYyyyJsonConverter : JsonConverter<DateOnly>
{
    private const string Format = "dd/MM/yyyy";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Data inválida. Formato esperado: {Format}");
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException($"Data inválida. Formato esperado: {Format}");
        }

        if (!DateOnly.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new JsonException($"Data inválida. Formato esperado: {Format}");
        }

        return date;
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}
