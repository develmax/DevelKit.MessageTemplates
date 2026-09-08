using System.Globalization;
using DevelKit.MessageTemplates;
using DevelKit.MessageTemplates.DynamicsCrm;
using DevelKit.MessageTemplates.Query;
using Microsoft.Xrm.Sdk;
using Model = DevelKit.MessageTemplates.Query.Base;
using Crm = Microsoft.Xrm.Sdk.Query;

Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
var count = 0;
void Equal<T>(T expected, T actual)
{
    if (!Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
}
void Check(string name, Action test) { test(); Console.WriteLine("PASS " + name); count++; }
async Task CheckAsync(string name, Func<Task> test) { await test(); Console.WriteLine("PASS " + name); count++; }
void Throws<T>(Action action) where T:Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
var id = Guid.Parse("00000000-0000-0000-0000-000000000042");
var schema = new TemplateSchema()
    .AddEntity("appointment", "activityid", "ownerid", "scheduledstart")
    .AddEntity("systemuser", "systemuserid", "fullname", "telephone1")
    .AddEntity("activityparty", "activitypartyid", "activityid", "partyid", "participationtypemask")
    .AddEntity("contact", "contactid", "fullname")
    .AddRelationship("appointment", "ownerid", "systemuser", new RelationshipStep("systemuser", "ownerid", "systemuserid"))
    .AddRelationship("appointment", "guests", "contact",
        new("activityparty", "activityid", "activityid", new Dictionary<string, object> { ["participationtypemask"] = 5 }),
        new("contact", "partyid", "contactid"));
var targets = new Dictionary<string, object> { ["appointment"] = id };
TextTemplateQuery Plan(string text) => TextTemplateQueryBuilder.Build(TextTemplateParser.Parse(text), targets, schema);

Check("CRM translation preserves root id, columns and shared join", () =>
{
    var plan = Plan("{{appointment:scheduledstart}}{{appointment:ownerid{{systemuser:fullname}}}}{{appointment:ownerid{{systemuser:telephone1}}}}");
    var sdk = CrmQueryTranslator.Translate(plan.SubQueries[0]);
    Equal("appointment", sdk.EntityName);
    Equal("activityid", sdk.Criteria.Conditions[0].AttributeName);
    Equal(id, (Guid)sdk.Criteria.Conditions[0].Values[0]);
    Equal(1, sdk.TopCount);
    Equal(1, sdk.LinkEntities.Count);
    Equal(2, sdk.LinkEntities[0].Columns.Columns.Count);
    Equal("appointment", sdk.LinkEntities[0].LinkFromEntityName);
    Equal("systemuserid", sdk.LinkEntities[0].LinkToAttributeName);
    Equal(Crm.JoinOperator.LeftOuter, sdk.LinkEntities[0].JoinOperator);
    Equal("t1", sdk.LinkEntities[0].EntityAlias);
    Equal(false, sdk.NoLock);
});
Check("activityparty path preserves role and nested parent entity", () =>
{
    var sdk = CrmQueryTranslator.Translate(Plan("{{appointment:guests{{contact:fullname}}}}").SubQueries[0]);
    var link = sdk.LinkEntities[0];
    Equal("activityparty", link.LinkToEntityName);
    Equal(5, link.LinkCriteria.Conditions[0].Values[0]);
    Equal("activityparty", link.LinkEntities[0].LinkFromEntityName);
    Equal("contact", link.LinkEntities[0].LinkToEntityName);
    Equal("t2", link.LinkEntities[0].EntityAlias);
});
Check("null equality becomes CRM Null condition", () =>
{
    var model = new Model.QueryExpression("contact");
    model.Criteria.AddCondition("name", Model.ConditionOperator.Equal, new object[] { null! });
    var sdk = CrmQueryTranslator.Translate(model);
    Equal(Crm.ConditionOperator.Null, sdk.Criteria.Conditions[0].Operator);
    Equal(0, sdk.Criteria.Conditions[0].Values.Count);
});
Check("OR and explicit NoLock are preserved", () =>
{
    var model = new Model.QueryExpression("contact") { NoLock = true };
    model.Criteria.FilterOperator = Model.LogicalOperator.Or;
    model.Criteria.AddCondition("name", Model.ConditionOperator.Equal, "A");
    model.Criteria.AddCondition("name", Model.ConditionOperator.Equal, "B");
    var sdk = CrmQueryTranslator.Translate(model);
    Equal(Crm.LogicalOperator.Or, sdk.Criteria.FilterOperator);
    Equal(true, sdk.NoLock);
});
Check("unsupported operators fail", () =>
{
    var model = new Model.QueryExpression("contact");
    model.Criteria.AddCondition("name", (Model.ConditionOperator)99, "A");
    Throws<NotSupportedException>(() => CrmQueryTranslator.Translate(model));
});
Check("schema rejects unlisted fields before CRM execution", () =>
    Throws<ArgumentException>(() => Plan("{{appointment:secret}}")));
Check("schema rejects unlisted relationship", () =>
    Throws<ArgumentException>(() => Plan("{{appointment:private{{contact:fullname}}}}")));
Check("schema rejects invalid relationship columns", () =>
    Throws<ArgumentException>(() => schema.AddRelationship("appointment", "bad", "contact", new RelationshipStep("contact", "missing", "contactid"))));
await CheckAsync("real engine renders via CRM SDK provider on net452", async () =>
{
    var fake = new FakeService(_ => Rows(new Entity("appointment")
    {
        ["scheduledstart"] = new DateTime(2026, 9, 15),
        ["t1.fullname"] = new AliasedValue("systemuser", "fullname", "Анна")
    }));
    var result = await new TemplateEngine(schema, new CrmTemplateDataProvider(fake)).RenderAsync(
        "{{appointment:ownerid{{systemuser:fullname}}}}: {{appointment:scheduledstart(dd.MM.yyyy)}}", targets);
    Equal("Анна: 15.09.2026", result);
    Equal(1, fake.Reads);
});
await CheckAsync("SDK values unwrapped without losing aliases or DateTime kind", async () =>
{
    var date = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
    var fake = new FakeService(_ => Rows(new Entity("contact")
    {
        ["t1.choice"] = new AliasedValue("contact", "choice", new OptionSetValue(7)),
        ["money"] = new Money(12.34m), ["ref"] = new EntityReference("contact", id),
        ["date"] = date, ["missing"] = null
    }));
    var values = (await new CrmTemplateDataProvider(fake).ReadAsync(new("contact"), default))!;
    Equal(7, values["t1.choice"]);
    Equal(12.34m, values["money"]);
    Equal(id, values["ref"]);
    Equal(DateTimeKind.Utc, ((DateTime)values["date"]!).Kind);
    Equal<object?>(null, values["missing"]);
});
await CheckAsync("empty CRM collection means missing row", async () =>
{
    var provider = new CrmTemplateDataProvider(new FakeService(_ => Rows()));
    Equal<IReadOnlyDictionary<string, object?>?>(null, await provider.ReadAsync(new("contact"), default));
});
await CheckAsync("pre-cancelled call does not reach CRM", async () =>
{
    using var cts = new CancellationTokenSource();
    cts.Cancel();
    var fake = new FakeService(_ => Rows());
    try { await new CrmTemplateDataProvider(fake).ReadAsync(new("contact"), cts.Token); }
    catch (OperationCanceledException) { Equal(0, fake.Reads); return; }
    throw new Exception("Cancellation expected");
});
await CheckAsync("service failures propagate unchanged", async () =>
{
    var original = new InvalidOperationException("CRM unavailable");
    var provider = new CrmTemplateDataProvider(new FakeService(_ => throw original));
    try { await provider.ReadAsync(new("contact"), default); }
    catch (InvalidOperationException e) { Equal(true, ReferenceEquals(original,e)); return; }
    throw new Exception("Failure expected");
});
await CheckAsync("alias repository reads paging cookie and inactive entries", async () =>
{
    var fake = new FakeService(query =>
    {
        Equal("alias_catalog", query.EntityName);
        if (query.PageInfo.PageNumber == 1)
            return new EntityCollection(new List<Entity> { new("alias_catalog")
            { ["name"] = "{{a:b}}", ["expansion"] = "{{contact:fullname}}", ["statecode"] = new OptionSetValue(0) } })
            { MoreRecords = true, PagingCookie = "cookie" };
        Equal(2, query.PageInfo.PageNumber);
        Equal("cookie", query.PageInfo.PagingCookie);
        return Rows(new Entity("alias_catalog")
        { ["name"] = "{{a:c}}", ["expansion"] = "hidden", ["statecode"] = new OptionSetValue(1) });
    });
    var repo = new CrmTemplateAliasRepository(fake, "alias_catalog", "name", "expansion");
    var definitions = await repo.LoadAsync(default);
    Equal(2, definitions.Count);
    Equal("{{contact:fullname}}", TemplateAliases.Expand("{{a:b}}{{a:c}}", definitions));
    Equal(2, fake.Reads);
});
await CheckAsync("missing alias name is rejected", async () =>
{
    var repo = new CrmTemplateAliasRepository(new FakeService(_ => Rows(new Entity("alias"))), "alias", "name", "expression");
    try { await repo.LoadAsync(default); } catch (FormatException) { return; }
    throw new Exception("Format failure expected");
});
Console.WriteLine($"Passed {count} CRM regression checks.");
static EntityCollection Rows(params Entity[] entities) => new(new List<Entity>(entities));

sealed class FakeService(Func<Crm.QueryExpression, EntityCollection> read) : IOrganizationService
{
    public int Reads { get; private set; }
    public EntityCollection RetrieveMultiple(Crm.QueryBase query) { Reads++; return read((Crm.QueryExpression)query); }
    public Guid Create(Entity entity) => throw new NotSupportedException();
    public void Update(Entity entity) => throw new NotSupportedException();
    public void Delete(string entityName, Guid id) => throw new NotSupportedException();
    public Entity Retrieve(string entityName, Guid id, Crm.ColumnSet columns) => throw new NotSupportedException();
    public OrganizationResponse Execute(OrganizationRequest request) => throw new NotSupportedException();
    public void Associate(string entityName, Guid id, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
    public void Disassociate(string entityName, Guid id, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
}