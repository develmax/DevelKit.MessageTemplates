namespace DevelKit.MessageTemplates.Query;

/// <summary>Явный список допустимых сущностей, полей и связей.</summary>
/// <remarks>Сначала зарегистрируйте сущности, затем связи. Завершите настройку до конкурентного чтения.</remarks>
public sealed class TemplateSchema : IQueryMetadata
{
    private readonly Dictionary<string, string> keys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> fields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<RelationshipStep>> relationships = new(StringComparer.Ordinal);

    public TemplateSchema AddEntity(string entity, string primaryKey, params string[] allowedFields)
    {
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(primaryKey))
            throw new ArgumentException("Entity and primary key are required.");
        keys.Add(entity, primaryKey);
        fields.Add(entity, new HashSet<string>(allowedFields, StringComparer.Ordinal) { primaryKey });
        return this;
    }

    public TemplateSchema AddRelationship(string entity, string field, string targetEntity, params RelationshipStep[] steps)
    {
        if (steps.Length == 0 || steps[steps.Length - 1].TargetEntity != targetEntity)
            throw new ArgumentException("Relationship must reach its target.");
        var current = entity;
        foreach (var step in steps)
        {
            ValidateField(current, step.FromColumn);
            ValidateField(step.TargetEntity, step.ToColumn);
            if (step.Filters != null)
                foreach (var filter in step.Filters) ValidateField(step.TargetEntity, filter.Key);
            current = step.TargetEntity;
        }
        // Копируем цепочку и словари: изменение переданной конфигурации не должно менять путь чтения.
        relationships.Add(Key(entity, field, targetEntity), steps.Select(s => s with
        {
            Filters = s.Filters == null ? null : s.Filters.ToDictionary(p => p.Key, p => p.Value)
        }).ToArray());
        return this;
    }

    public string GetPrimaryKey(string entity) => keys.TryGetValue(entity, out var key)
        ? key : throw new ArgumentException($"Entity is not allowed: {entity}.");

    public void ValidateField(string entity, string field)
    {
        if (!fields.TryGetValue(entity, out var allowed) || !allowed.Contains(field))
            throw new ArgumentException($"Field is not allowed: {entity}.{field}.");
    }

    public IReadOnlyList<RelationshipStep> GetRelationship(string entity, string field, string targetEntity) =>
        relationships.TryGetValue(Key(entity, field, targetEntity), out var path)
            ? path : throw new ArgumentException($"Relationship is not allowed: {entity}.{field} -> {targetEntity}.");

    private static string Key(string entity, string field, string target) => entity.Length + ":" + entity + field.Length + ":" + field + target;
}