using DevelKit.MessageTemplates.Parameters;
using DevelKit.MessageTemplates.Query;

namespace DevelKit.MessageTemplates;

/// <summary>Соединяет раскрытие каталога, разбор, планирование, чтение данных и форматирование.</summary>
public sealed class TemplateEngine(IQueryMetadata metadata, ITemplateDataProvider provider)
{
    /// <summary>Готовит текст для указанных корневых записей.</summary>
    /// <param name="text">Шаблон с техническими или именованными параметрами.</param>
    /// <param name="targets">Имя корневой сущности и идентификатор записи для каждой группы параметров.</param>
    /// <param name="aliases">Каталог для однократного раскрытия перед разбором.</param>
    /// <param name="transform">Подготовка значения: например, нормализация или перевод времени.</param>
    /// <param name="cancellationToken">Передаётся поставщику данных. Поддержка отмены зависит от его реализации.</param>
    /// <remarks>Проверка длины, выбор получателя и отправка сообщения остаются в приложении.</remarks>
    public async Task<string> RenderAsync(string text, IReadOnlyDictionary<string, object> targets,
        IEnumerable<TemplateAlias>? aliases = null,
        Func<TextTemplateParameter, object?, object?>? transform = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        text = TemplateAliases.Expand(text, aliases ?? []);
        var template = TextTemplateParser.Parse(text);
        if (!TextTemplateValidator.Validate(template, out var errors))
            throw new FormatException(string.Join(Environment.NewLine, errors));
        var plan = TextTemplateQueryBuilder.Build(template, targets, metadata);
        var values = new Dictionary<TextTemplateParameter, object?>();
        // Один запрос на корневую группу. Последовательное выполнение не требует
        // от переданного поставщика поддержки конкурентного доступа к соединению/сервису.
        foreach (var query in plan.SubQueries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = await provider.ReadAsync(query, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"No data for {query.EntityName}.");
            foreach (var mapping in plan.TemplateMapping.Where(m => ReferenceEquals(m.Value.Key, query)))
            {
                // Отсутствующая колонка означает null. Проверка выше считает отсутствие самой строки ошибкой.
                row.TryGetValue(mapping.Value.Value, out var value);
                values.Add(mapping.Key, transform is null ? value : transform(mapping.Key, value));
            }
        }
        return TextTemplateFormatter.Format(template, p => values[p]);
    }
}