using DevelKit.MessageTemplates.Query.Base;

namespace DevelKit.MessageTemplates.Query;

/// <summary>Добавляет к чтению данных метаданные, зависимые поля и подготовку CLR-значений.</summary>
/// <remarks>Исходный план не изменяется. Применим к SQL-провайдеру и CRM-провайдеру,
/// который уже раскрывает SDK-обёртки. Декоратор должен предшествовать форматированию.</remarks>
public sealed class PreparingTemplateDataProvider(
    ITemplateDataProvider source, ITemplateValueMetadataProvider metadata, IQueryMetadata schema) : ITemplateDataProvider
{
    public async Task<IReadOnlyDictionary<string, object?>?> ReadAsync(
        QueryExpression query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var copy = Clone(query);
        var fields = new List<TemplateResultField>();
        var locations = new Dictionary<string, FieldLocation>(StringComparer.Ordinal)
        { [""] = new FieldLocation(copy.EntityName, copy.ColumnSet) };
        AddFields(copy.EntityName, "", copy.ColumnSet);
        Visit(copy.LinkEntities);
        var rules = await metadata.LoadAsync(fields, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var field in fields)
        {
            if (!rules.TryGetValue(field.Column, out var rule))
                throw new InvalidOperationException($"Missing value metadata: {field.Column}.");
            if (rule.OffsetColumn is not { } dependency) continue;
            if (rule.Kind != TemplateValueKind.UtcDateTime)
                throw new InvalidOperationException("An offset can only be applied to a UTC date.");
            var dot = dependency.IndexOf('.');
            var alias = dot < 0 ? "" : dependency.Substring(0, dot);
            var name = dot < 0 ? dependency : dependency.Substring(dot + 1);
            if (!locations.TryGetValue(alias, out var location))
                throw new InvalidOperationException($"Offset relationship is not in the query: {alias}.");
            // Служебные поля проходят ту же проверку доступа, что и поля из шаблона.
            schema.ValidateField(location.Entity, name);
            location.Columns.Columns.Add(name);
        }
        var row = await source.ReadAsync(copy, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (row is null) return null;
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            row.TryGetValue(field.Column, out var value);
            result.Add(field.Column, TemplateValueConverter.Convert(value, rules[field.Column], row));
        }
        return result;

        void AddFields(string entity, string prefix, ColumnSet columns)
        {
            foreach (var name in columns.Columns)
            {
                schema.ValidateField(entity, name);
                fields.Add(new(entity, name, prefix.Length == 0 ? name : prefix + "." + name));
            }
        }
        void Visit(IEnumerable<LinkEntity> links)
        {
            foreach (var link in links)
            {
                var alias = link.EntityAlias ?? throw new InvalidOperationException("A link alias is required.");
                locations.Add(alias, new FieldLocation(link.LinkToEntityName, link.Columns));
                AddFields(link.LinkToEntityName, alias, link.Columns);
                Visit(link.LinkEntities);
            }
        }
    }

    private sealed record FieldLocation(string Entity, ColumnSet Columns);

    private static QueryExpression Clone(QueryExpression query) => new(query.EntityName)
    {
        ColumnSet = new ColumnSet(query.ColumnSet.Columns.ToArray()), Criteria = query.Criteria,
        TopCount = query.TopCount, NoLock = query.NoLock,
        LinkEntities = query.LinkEntities.Select(CloneLink).ToList()
    };

    private static LinkEntity CloneLink(LinkEntity link) => new()
    {
        EntityAlias = link.EntityAlias, JoinOperator = link.JoinOperator,
        LinkFromEntityName = link.LinkFromEntityName, LinkFromAttributeName = link.LinkFromAttributeName,
        LinkToEntityName = link.LinkToEntityName, LinkToAttributeName = link.LinkToAttributeName,
        LinkCriteria = link.LinkCriteria, Columns = new ColumnSet(link.Columns.Columns.ToArray()),
        LinkEntities = link.LinkEntities.Select(CloneLink).ToList()
    };
}
