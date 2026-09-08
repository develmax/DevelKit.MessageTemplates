using DevelKit.MessageTemplates;
using DevelKit.MessageTemplates.Query;
using DevelKit.MessageTemplates.Query.Base;
using DevelKit.MessageTemplates.SqlServer;

var template = "Здравствуйте, {{meeting:guest{{person:name}}}}! Встреча {{meeting:date}}.";
// Короткий параметр даты раскрывается до разбора в поле с форматом .NET.
var aliases = new[] { new TemplateAlias("{{meeting:date}}", "{{meeting:startsAt(dd.MM.yyyy HH:mm)}}") };
var engine = new TemplateEngine(new MeetingMetadata(), new DemoProvider());
Console.WriteLine(await engine.RenderAsync(template, new Dictionary<string, object> { ["meeting"] = 42 }, aliases));

sealed class MeetingMetadata : IQueryMetadata
{
    public string GetPrimaryKey(string entity) => entity is "meeting" or "person" ? "id" : throw new ArgumentException(entity);
    public void ValidateField(string entity, string field)
    {
        var allowed = entity switch
        {
            "meeting" => new[] { "id", "guestId", "startsAt" },
            "person" => new[] { "id", "name" },
            _ => []
        };
        if (!allowed.Contains(field)) throw new ArgumentException($"{entity}.{field}");
    }
    public IReadOnlyList<RelationshipStep> GetRelationship(string entity, string field, string targetEntity) =>
        (entity, field, targetEntity) == ("meeting", "guest", "person")
            ? [new("person", "guestId", "id")] : throw new ArgumentException("Unknown relationship");
}

sealed class DemoProvider : ITemplateDataProvider
{
    public Task<IReadOnlyDictionary<string, object?>?> ReadAsync(QueryExpression query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Показываем SQL, но не открываем базу. Демо-строка использует псевдонимы из плана.
        // В приложении здесь выполняется команда и читаются значения результата.
        var sql = SqlServerQueryGenerator.Generate(query);
        Console.WriteLine(sql.Text);
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?>
        {
            ["startsAt"] = new DateTime(2026, 9, 15, 14, 30, 0),
            ["t1.name"] = "Анна"
        };
        return Task.FromResult<IReadOnlyDictionary<string, object?>?>(row);
    }
}