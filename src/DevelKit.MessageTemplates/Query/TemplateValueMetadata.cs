namespace DevelKit.MessageTemplates.Query;

/// <summary>Тип значения после чтения источника, до выполнения условий шаблона.</summary>
public enum TemplateValueKind { Raw, String, Boolean, Int32, Int64, Decimal, Double, Single, Guid, UtcDateTime }

/// <summary>Поле результата с исходной сущностью и псевдонимом колонки.</summary>
public sealed record TemplateResultField(string Entity, string Field, string Column);

/// <summary>Правила подготовки одного поля. OffsetColumn содержит смещение от UTC в минутах.</summary>
/// <remarks>OffsetColumn может указывать на корневую колонку или alias.field существующей связи.
/// FallbackTimeZone применяется, если смещение отсутствует. Без обоих правил дата остаётся UTC.
/// NullSentinel применяется только к этому полю. Метаданные не являются пользовательским текстом.</remarks>
public sealed record TemplateValueMetadata(TemplateValueKind Kind,
    string? NullSentinel = null, string? OffsetColumn = null, TimeZoneInfo? FallbackTimeZone = null);

/// <summary>Загружает правила для всех выбранных полей одним обращением.</summary>
/// <remarks>Ключ результата — псевдоним колонки. Для каждого запрошенного поля требуется правило,
/// включая явный Raw для значений без преобразования. Реализация управляет кешем метаданных.</remarks>
public interface ITemplateValueMetadataProvider
{
    Task<IReadOnlyDictionary<string, TemplateValueMetadata>> LoadAsync(
        IReadOnlyList<TemplateResultField> fields, CancellationToken cancellationToken);
}