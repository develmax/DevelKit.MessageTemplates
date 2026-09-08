# DevelKit.MessageTemplates.DynamicsCrm

[Русский](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/dynamics-crm.md) | **English**

A Dynamics CRM 2015 adapter targeting .NET Framework 4.5.2 and Microsoft.CrmSdk.CoreAssemblies 7.0.0.1. It is a separate project and package and does not require SQL Server.

## Integration

```csharp
using DevelKit.MessageTemplates;
using DevelKit.MessageTemplates.Query;
using DevelKit.MessageTemplates.DynamicsCrm;

var schema = new TemplateSchema()
    .AddEntity("appointment", "activityid", "ownerid", "scheduledstart")
    .AddEntity("systemuser", "systemuserid", "fullname")
    .AddRelationship("appointment", "ownerid", "systemuser",
        new RelationshipStep("systemuser", "ownerid", "systemuserid"));

var engine = new TemplateEngine(schema, new CrmTemplateDataProvider(service));
var result = await engine.RenderAsync(
    "{{appointment:ownerid{{systemuser:fullname}}}}: {{appointment:scheduledstart(dd.MM.yyyy)}}",
    new Dictionary<string, object> { ["appointment"] = appointmentId });
```

service is the application's IOrganizationService. The caller manages the connection, authentication, permissions, and service lifetime.

TemplateSchema explicitly defines available entities, fields, primary keys, and relationships. CrmQueryTranslator translates the plan into actual Microsoft.Xrm.Sdk.Query types. CrmTemplateDataProvider calls RetrieveMultiple and unwraps AliasedValue, OptionSetValue, and Money. EntityReference becomes a Guid. Dates are retained without automatic time zone conversion.

Define activityparty relationships as chains of RelationshipStep with a participationtypemask filter. The adapter has no fixed organization-specific model.

For aliases, use CrmTemplateAliasRepository with the entity and field names of your catalog. Wrap it in CachedTemplateAliases if needed. The repository follows PagingCookie and reads inactive records so that disabled parameters retain their defined behavior.

## Limitations

- The CRM 2015 SDK is synchronous. ReadAsync implements the core contract, but the network call remains synchronous. Cancellation is checked before and after the call and does not interrupt a request already in progress.
- Checks use a fake IOrganizationService and actual SDK types. Connections to a CRM server and execution in the plugin sandbox have not been verified.
- This is an application library, not a registered CRM plugin. Plugin assembly signing and deployment have not been performed.
- Modern Dataverse ServiceClient is not included.
- Configure the schema before concurrent use.
- Single-row selection and formatting limits are described in the [compatibility documentation](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/en/compatibility.md).

[Official CRM SDK package](https://www.nuget.org/packages/Microsoft.CrmSdk.CoreAssemblies/7.0.0.1).
