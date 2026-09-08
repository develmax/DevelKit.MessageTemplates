using DevelKit.MessageTemplates;
using DevelKit.MessageTemplates.DynamicsCrm;
using DevelKit.MessageTemplates.Query;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

// Явно разрешаем поля и путь от встречи к пользователю-владельцу.
// Настройка схемы не требует обращения к CRM.
var schema = new TemplateSchema()
    .AddEntity("appointment", "activityid", "ownerid", "scheduledstart")
    .AddEntity("systemuser", "systemuserid", "fullname")
    .AddRelationship("appointment", "ownerid", "systemuser",
        new RelationshipStep("systemuser", "ownerid", "systemuserid"));
var service = new DemoOrganizationService();
var engine = new TemplateEngine(schema, new CrmTemplateDataProvider(service));
Console.WriteLine(await engine.RenderAsync(
    "Ответственный: {{appointment:ownerid{{systemuser:fullname}}}}. Дата: {{appointment:scheduledstart(dd.MM.yyyy)}}.",
    new Dictionary<string, object> { ["appointment"] = Guid.Parse("00000000-0000-0000-0000-000000000042") }));

// Автономный пример: замените этот объект сервисом IOrganizationService своего приложения.
// Возвращаем структуру SDK с AliasedValue, чтобы пройти тот же путь раскрытия значений.
sealed class DemoOrganizationService : IOrganizationService
{
    public EntityCollection RetrieveMultiple(QueryBase query)
    {
        var sdk = (QueryExpression)query;
        Console.WriteLine($"CRM SDK: {sdk.EntityName}, links: {sdk.LinkEntities.Count}; no SQL.");
        return new EntityCollection(new List<Entity>
        {
            new("appointment")
            {
                ["scheduledstart"] = new DateTime(2026, 9, 15),
                ["t1.fullname"] = new AliasedValue("systemuser", "fullname", "Анна")
            }
        });
    }
    public Guid Create(Entity entity) => throw new NotSupportedException();
    public void Update(Entity entity) => throw new NotSupportedException();
    public void Delete(string entityName, Guid id) => throw new NotSupportedException();
    public Entity Retrieve(string entityName, Guid id, ColumnSet columns) => throw new NotSupportedException();
    public OrganizationResponse Execute(OrganizationRequest request) => throw new NotSupportedException();
    public void Associate(string entityName, Guid id, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
    public void Disassociate(string entityName, Guid id, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
}