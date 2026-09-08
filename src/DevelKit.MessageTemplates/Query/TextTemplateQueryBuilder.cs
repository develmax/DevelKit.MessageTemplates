using DevelKit.MessageTemplates.Parameters;
using DevelKit.MessageTemplates.Query.Base;

namespace DevelKit.MessageTemplates.Query;

/// <summary>Собирает план чтения из дерева, объединяя одинаковые колонки и пути связей.</summary>
public static class TextTemplateQueryBuilder
{
    /// <summary>Для каждой корневой сущности создаёт запрос по идентификатору из targets.</summary>
    /// <remarks>Метаданные должны отклонять поля и связи, не разрешённые приложением.</remarks>
    public static TextTemplateQuery Build(TextTemplate template,
        IReadOnlyDictionary<string, object> targets, IQueryMetadata metadata)
    {
        var result = new TextTemplateQuery();
        foreach (var group in template.Parameters.GroupBy(p => p.EntityName!))
        {
            if (!targets.TryGetValue(group.Key, out var id))
                throw new InvalidOperationException($"Missing target: {group.Key}.");
            var query = new QueryExpression(group.Key) { TopCount = 1 };
            query.Criteria.AddCondition(metadata.GetPrimaryKey(group.Key), ConditionOperator.Equal, id);
            var root = new LinkEntity { EntityAlias = group.Key, LinkToEntityName = group.Key };
            var aliasIndex = 0;
            foreach (var parameter in group) Visit(parameter, root);
            query.ColumnSet = root.Columns;
            query.LinkEntities = root.LinkEntities;
            result.SubQueries.Add(query);

            void Visit(TextTemplateParameter parameter, LinkEntity parent)
            {
                if (parameter.Type == TextTemplateParameterType.Operator)
                {
                    // До чтения данных условие неизвестно: план содержит обе ветви.
                    // Поэтому условие шаблона нельзя использовать как ограничение доступа.
                    foreach (var child in parameter.SubParameters!) Visit(child, parent);
                    return;
                }
                if (!parameter.HasSubParameters)
                {
                    metadata.ValidateField(parent.LinkToEntityName, parameter.FieldName!);
                    parent.Columns.Columns.Add(parameter.FieldName!);
                    var column = ReferenceEquals(root, parent)
                        ? parameter.FieldName! : $"{parent.EntityAlias}.{parameter.FieldName}";
                    // Узел остаётся отдельным вхождением текста даже при общей колонке.
                    result.TemplateMapping.Add(parameter, new(query, column));
                    return;
                }
                var nested = parameter.SubParameters![0];
                var steps = metadata.GetRelationship(parent.LinkToEntityName, parameter.FieldName!, nested.EntityName!);
                if (steps.Count == 0 || steps[steps.Count - 1].TargetEntity != nested.EntityName)
                    throw new InvalidOperationException("Relationship must reach the requested entity.");
                var current = parent;
                foreach (var step in steps)
                {
                    metadata.ValidateField(current.LinkToEntityName, step.FromColumn);
                    metadata.ValidateField(step.TargetEntity, step.ToColumn);
                    var filters = step.Filters ?? new Dictionary<string, object>();
                    foreach (var filter in filters) metadata.ValidateField(step.TargetEntity, filter.Key);
                    // Фильтры входят в идентичность связи: разные роли участника дают разные пути.
                    // Ищем только среди детей текущего узла, чтобы не смешать соседние ветви.
                    var link = current.LinkEntities.FirstOrDefault(l =>
                        l.LinkToEntityName == step.TargetEntity &&
                        l.LinkFromAttributeName == step.FromColumn &&
                        l.LinkToAttributeName == step.ToColumn &&
                        l.LinkCriteria.Conditions.Count == filters.Count &&
                        l.LinkCriteria.Conditions.All(c => c.Operator == ConditionOperator.Equal &&
                            filters.TryGetValue(c.AttributeName, out var value) && Equals(c.Values[0], value)));
                    if (link is null)
                    {
                        link = current.AddLink(step.TargetEntity, step.FromColumn, step.ToColumn, JoinOperator.LeftOuter);
                        link.EntityAlias = $"t{++aliasIndex}";
                        foreach (var filter in filters)
                            link.LinkCriteria.AddCondition(filter.Key, ConditionOperator.Equal, filter.Value);
                    }
                    current = link;
                }
                Visit(nested, current);
            }
        }
        return result;
    }
}