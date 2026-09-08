using DevelKit.MessageTemplates.Query.Base;

namespace DevelKit.MessageTemplates.Query;

/// <summary>Читает одну строку: ключи совпадают с псевдонимами колонок плана, значения имеют CLR-типы.</summary>
/// <remarks>
/// null вместо строки означает отсутствие корневой записи. Отсутствующий ключ означает пустое поле.
/// Адаптер раскрывает обёртки источника данных и подготавливает время с учётом контекста приложения.
/// </remarks>
public interface ITemplateDataProvider
{
    Task<IReadOnlyDictionary<string, object?>?> ReadAsync(QueryExpression query, CancellationToken cancellationToken);
}