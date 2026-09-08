using System.Globalization;

namespace DevelKit.MessageTemplates.Query;

/// <summary>Преобразует значения по явным метаданным, сохраняя правила сравнения форматтера.</summary>
public static class TemplateValueConverter
{
    public static object? Convert(object? value, TemplateValueMetadata metadata,
        IReadOnlyDictionary<string, object?> row)
    {
        if (value is null || value == DBNull.Value) return null;
        if (value is string text && metadata.NullSentinel is { } sentinel &&
            string.Equals(text, sentinel, StringComparison.OrdinalIgnoreCase)) return null;
        return metadata.Kind switch
        {
            TemplateValueKind.Raw => value,
            TemplateValueKind.String => value is string s ? s : throw Invalid(value, metadata.Kind),
            TemplateValueKind.Boolean => value is bool b ? b : Integer(value) switch
            { 0 => false, 1 => true, _ => throw Invalid(value, metadata.Kind) },
            TemplateValueKind.Int32 => checked((int)Integer(value)),
            TemplateValueKind.Int64 => Integer(value),
            TemplateValueKind.Decimal => Numeric(value, metadata.Kind, v => System.Convert.ToDecimal(v, CultureInfo.InvariantCulture)),
            TemplateValueKind.Double => Numeric(value, metadata.Kind, v => System.Convert.ToDouble(v, CultureInfo.InvariantCulture)),
            TemplateValueKind.Single => Numeric(value, metadata.Kind, v => System.Convert.ToSingle(v, CultureInfo.InvariantCulture)),
            TemplateValueKind.Guid => value is Guid id ? id : throw Invalid(value, metadata.Kind),
            TemplateValueKind.UtcDateTime => Date(value, metadata, row),
            _ => throw new ArgumentOutOfRangeException(nameof(metadata))
        };
    }

    private static object Date(object value, TemplateValueMetadata metadata, IReadOnlyDictionary<string, object?> row)
    {
        // SQL datetime2 не хранит Kind. Метаданные явно определяют его как UTC.
        // Local нельзя молча переобозначить: нужна зона исходного значения в адаптере.
        var utc = value switch
        {
            DateTimeOffset offset => offset.UtcDateTime,
            DateTime date when date.Kind != DateTimeKind.Local => DateTime.SpecifyKind(date, DateTimeKind.Utc),
            _ => throw Invalid(value, TemplateValueKind.UtcDateTime)
        };
        if (metadata.OffsetColumn is { } column)
        {
            if (!row.TryGetValue(column, out var offset))
                throw new InvalidOperationException($"Required offset column was not returned: {column}.");
            if (offset is not null && offset != DBNull.Value)
            {
                var minutes = Integer(offset);
                if (minutes < -840 || minutes > 840) throw new ArgumentOutOfRangeException(nameof(row), "UTC offset must be within 14 hours.");
                // Время получателя не является локальным временем машины, поэтому Unspecified.
                return DateTime.SpecifyKind(utc.AddMinutes(minutes), DateTimeKind.Unspecified);
            }
        }
        return metadata.FallbackTimeZone is { } zone
            ? DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, zone), DateTimeKind.Unspecified)
            : utc;
    }

    private static long Integer(object value) => value switch
    {
        sbyte v => v, byte v => v, short v => v, ushort v => v, int v => v,
        uint v => v, long v => v, ulong v => checked((long)v),
        _ => throw Invalid(value, TemplateValueKind.Int64)
    };

    private static object Numeric(object value, TemplateValueKind kind, Func<object, object> convert) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal
            ? convert(value) : throw Invalid(value, kind);

    private static ArgumentException Invalid(object value, TemplateValueKind kind) =>
        new($"Cannot prepare {value.GetType().Name} as {kind}.");
}