namespace DevelKit.MessageTemplates.Query;

/// <summary>Схема приложения: ключи сущностей, разрешённые поля и пути связей.</summary>
/// <remarks>Неизвестные имена должны отклоняться до обращения к источнику данных.</remarks>
public interface IQueryMetadata
{
    string GetPrimaryKey(string entity);
    void ValidateField(string entity, string field);
    IReadOnlyList<RelationshipStep> GetRelationship(string entity, string field, string targetEntity);
}

/// <summary>Один шаг от FromColumn текущей сущности к ToColumn сущности TargetEntity.</summary>
/// <remarks>Filters применяются к целевой сущности шага, например для выбора роли участника.</remarks>
public sealed record RelationshipStep(
    string TargetEntity, string FromColumn, string ToColumn,
    IReadOnlyDictionary<string, object>? Filters = null);