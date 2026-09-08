using System.Globalization;
using DevelKit.MessageTemplates;
using DevelKit.MessageTemplates.Parameters;
using DevelKit.MessageTemplates.Query;
using DevelKit.MessageTemplates.Query.Base;
using DevelKit.MessageTemplates.SqlServer;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var passed = 0;
void Check(string name, Action test) { test(); Console.WriteLine("PASS " + name); passed++; }
async Task CheckAsync(string name, Func<Task> test) { await test(); Console.WriteLine("PASS " + name); passed++; }
void Equal<T>(T expected, T actual) { if (!Equals(expected, actual)) throw new Exception($"Expected {expected}; actual {actual}"); }
void Throws<T>(Action test) where T : Exception
{
    try { test(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
string Render(string? text, Func<TextTemplateParameter, object?>? value = null) =>
    TextTemplateFormatter.Format(TextTemplateParser.Parse(text), value ?? (_ => "X"));
Check("plain text", () => Equal("hello", Render("hello")));
Check("null input", () => Equal("", Render(null)));
Check("empty input", () => Equal("", Render("")));
Check("simple parameter", () => Equal("Hi X!", Render("Hi {{person:name}}!")));
Check("date format", () => Equal("15.09.2026", Render("{{meeting:date(dd.MM.yyyy)}}", _ => new DateTime(2026, 9, 15))));
Check("nested field", () => Equal("name", Render("{{meeting:guest{{person:name}}}}", p => p.FieldName)));
Check("malformed text retained", () => Equal("{{broken}}", Render("{{broken}}")));
Check("empty first parameter regression", () => Equal(" body", Render("{{person:name}} body", _ => null)));
Check("null removes preceding whitespace", () => Equal("A B", Render("A  {{person:name}} B", _ => null)));
Check("conditional true", () => Equal("yes", Render("{{x:if[[count > 2 ? yes ; no]]}}", p => p.FieldName == "count" ? 3 : p.FieldName)));
Check("conditional false", () => Equal("no", Render("{{x:if[[count > 2 ? yes ; no]]}}", p => p.FieldName == "count" ? 1 : p.FieldName)));
Check("nested conditional", () => Equal("yes", Render("{{x:if[[count > 2 ? if[[count = 3 ? yes ; no]] ; no]]}}", p => p.FieldName == "count" ? 3 : p.FieldName)));
Check("null equality", () => Equal("yes", Render("{{x:if[[value = null ? yes ; no]]}}", p => p.FieldName == "value" ? null : p.FieldName)));
Check("boolean literal", () => Equal("yes", Render("{{x:if[[value = true ? yes ; no]]}}", p => p.FieldName == "value" ? true : p.FieldName)));
Check("type sensitive equality retained", () => Equal("no", Render("{{x:if[[value = 3 ? yes ; no]]}}", p => p.FieldName == "value" ? 3L : p.FieldName)));
Check("mixed ordered comparison rejected", () => Throws<ArgumentException>(() => Render("{{x:if[[value > 2 ? yes ; no]]}}", p => p.FieldName == "value" ? 3L : p.FieldName)));
Check("alias expansion", () => Equal("{{x:value}}", TemplateAliases.Expand("{{PUBLIC:NAME}}", [new("{{public:name}}", "{{x:value}}")])));
Check("alias inactive", () => Equal("", TemplateAliases.Expand("{{x:a}}", [new("{{x:a}}", "value", false)])));
Check("alias single pass", () => Equal("{{x:b}}", TemplateAliases.Expand("{{x:a}}", [new("{{x:a}}", "{{x:b}}"), new("{{x:b}}", "value")])));
Check("duplicate alias last wins", () => Equal("B", TemplateAliases.Expand("{{x:a}}", [new("{{x:a}}", "A"), new("{{x:a}}", "B")])));
Check("depth limit", () => Throws<FormatException>(() => TextTemplateParser.Parse(string.Concat(Enumerable.Repeat("{{x:f", 70)) + "{{x:v}}" + string.Concat(Enumerable.Repeat("}}", 70)))));
var metadata = new TestMetadata();
var targets = new Dictionary<string, object> { ["x"] = 42 };
TextTemplateQuery Plan(string text) => TextTemplateQueryBuilder.Build(TextTemplateParser.Parse(text), targets, metadata);
Check("shared link and deduplicated column", () =>
{
    var plan = Plan("{{x:owner{{person:name}}}}{{x:owner{{person:name}}}}");
    Equal(1, plan.SubQueries[0].LinkEntities.Count);
    Equal(1, plan.SubQueries[0].LinkEntities[0].Columns.Columns.Count);
    Equal(2, plan.TemplateMapping.Count);
});
Check("conditional inside link", () =>
{
    var plan = Plan("{{x:owner{{person:if[[age > 18 ? name ; nickname]]}}}}");
    Equal(3, plan.TemplateMapping.Count);
    Equal(3, plan.SubQueries[0].LinkEntities[0].Columns.Columns.Count);
});
Check("bridge relation and filters", () =>
{
    var plan = Plan("{{x:members{{person:name}}}}");
    Equal("membership", plan.SubQueries[0].LinkEntities[0].LinkToEntityName);
    Equal(1, plan.SubQueries[0].LinkEntities[0].LinkCriteria.Conditions.Count);
    var sql = SqlServerQueryGenerator.Generate(plan.SubQueries[0]);
    Equal(true, sql.Text.Contains("[t1].[role] = @p0"));
    Equal("guest", sql.Parameters[0]);
    Equal(42, sql.Parameters[1]);
});
Check("missing target", () => Throws<InvalidOperationException>(() => Plan("{{y:name}}")));
Check("SQL parameterizes values", () =>
{
    var query = new QueryExpression("person") { ColumnSet = new("name") };
    query.Criteria.AddCondition("id", ConditionOperator.Equal, "'; DROP TABLE person;--");
    var sql = SqlServerQueryGenerator.Generate(query);
    Equal(false, sql.Text.Contains("DROP"));
    Equal(true, sql.Text.Contains("[r].[id] = @p0"));
    Equal(1, sql.Parameters.Count);
});
Check("SQL rejects identifiers", () => Throws<ArgumentException>(() => SqlServerQueryGenerator.Generate(new("person];DROP") { ColumnSet = new("name") })));
Check("SQL repeat generation does not mutate plan", () =>
{
    var query = Plan("{{x:owner{{person:name}}}}").SubQueries[0];
    Equal(SqlServerQueryGenerator.Generate(query).Text, SqlServerQueryGenerator.Generate(query).Text);
});
await CheckAsync("engine loads one row and formats", async () =>
{
    var provider = new TestProvider(new Dictionary<string, object?> { ["t1.name"] = "Анна", ["value"] = 7 });
    var engine = new TemplateEngine(metadata, provider);
    Equal("Анна: 7", await engine.RenderAsync("{{x:owner{{person:name}}}}: {{x:value}}", targets));
    Equal(1, provider.Calls);
});
await CheckAsync("transform belongs to application", async () =>
{
    var engine = new TemplateEngine(metadata, new TestProvider(new Dictionary<string, object?> { ["value"] = "unknown" }));
    Equal("", await engine.RenderAsync("{{x:value}}", targets, transform: (_, _) => null));
});
await CheckAsync("missing row is an error", async () =>
{
    try { await new TemplateEngine(metadata, new TestProvider(null)).RenderAsync("{{x:value}}", targets); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Missing row should fail");
});
await CheckAsync("cancellation is propagated", async () =>
{
    using var cts = new CancellationTokenSource();
    cts.Cancel();
    try { await new TemplateEngine(metadata, new TestProvider(null)).RenderAsync("hello", targets, cancellationToken: cts.Token); }
    catch (OperationCanceledException) { return; }
    throw new Exception("Cancellation should fail");
});
await CheckAsync("cache coalesces concurrent loads and invalidates", async () =>
{
    var repository = new TestRepository();
    var cache = new CachedTemplateAliases(repository);
    await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cache.ExpandAsync("{{x:a}}")));
    Equal(1, repository.Calls);
    cache.Invalidate();
    Equal("value", await cache.ExpandAsync("{{x:a}}"));
    Equal(2, repository.Calls);
});
await CheckAsync("cache TTL", async () =>
{
    var repository = new TestRepository();
    var clock = new TestClock();
    var cache = new CachedTemplateAliases(repository, TimeSpan.FromMinutes(1), clock.GetUtcNow);
    await cache.ExpandAsync("{{x:a}}");
    clock.Now = clock.Now.AddMinutes(2);
    await cache.ExpandAsync("{{x:a}}");
    Equal(2, repository.Calls);
});
await PreparationChecks.RunAsync();
Console.WriteLine($"Passed {passed} existing regression checks.");

sealed class TestMetadata : IQueryMetadata
{
    public string GetPrimaryKey(string entity) => "id";
    public void ValidateField(string entity, string field) { }
    public IReadOnlyList<RelationshipStep> GetRelationship(string entity, string field, string targetEntity) =>
        field == "members"
            ? [new("membership", "id", "parentId", new Dictionary<string, object> { ["role"] = "guest" }), new(targetEntity, "personId", "id")]
            : [new(targetEntity, field + "Id", "id")];
}
sealed class TestProvider(IReadOnlyDictionary<string, object?>? row) : ITemplateDataProvider
{
    public int Calls;
    public Task<IReadOnlyDictionary<string, object?>?> ReadAsync(QueryExpression query, CancellationToken cancellationToken)
    { Calls++; return Task.FromResult(row); }
}
sealed class TestRepository : ITemplateAliasRepository
{
    public int Calls;
    public async Task<IReadOnlyList<TemplateAlias>> LoadAsync(CancellationToken cancellationToken)
    { Calls++; await Task.Yield(); return [new("{{x:a}}", "value")]; }
}
sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    public override DateTimeOffset GetUtcNow() => Now;
}
