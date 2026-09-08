using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DevelKit.MessageTemplates.DynamicsCrm;

/// <summary>Читает каталог параметров из сущности и полей, заданных приложением.</summary>
/// <remarks>Загружает также неактивные определения, чтобы раскрытие могло удалить их из текста.</remarks>
public sealed class CrmTemplateAliasRepository : ITemplateAliasRepository
{
    private readonly IOrganizationService service;
    private readonly string entityName, nameField, expansionField, stateField;
    private readonly int inactiveState;

    public CrmTemplateAliasRepository(IOrganizationService service, string entityName,
        string nameField, string expansionField, string stateField = "statecode", int inactiveState = 1)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.entityName = Required(entityName);
        this.nameField = Required(nameField);
        this.expansionField = Required(expansionField);
        this.stateField = Required(stateField);
        this.inactiveState = inactiveState;
    }

    public Task<IReadOnlyList<TemplateAlias>> LoadAsync(CancellationToken cancellationToken)
    {
        var query = new QueryExpression(entityName)
        {
            ColumnSet = new ColumnSet(nameField, expansionField, stateField),
            PageInfo = new PagingInfo { PageNumber = 1, Count = 5000 }
        };
        var result = new List<TemplateAlias>();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = service.RetrieveMultiple(query);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var row in rows.Entities)
            {
                var name = row.GetAttributeValue<string>(nameField);
                if (string.IsNullOrWhiteSpace(name))
                    throw new FormatException("A CRM template alias has no name.");
                var expansion = row.GetAttributeValue<string>(expansionField) ?? string.Empty;
                var state = row.GetAttributeValue<OptionSetValue>(stateField);
                result.Add(new TemplateAlias(name, expansion, state?.Value != inactiveState));
            }
            if (!rows.MoreRecords) break;
            // Сохраняем cookie CRM: одна страница может содержать лишь часть каталога.
            query.PageInfo.PageNumber++;
            query.PageInfo.PagingCookie = rows.PagingCookie;
        } while (true);
        return Task.FromResult<IReadOnlyList<TemplateAlias>>(result);
    }

    private static string Required(string value) => !string.IsNullOrWhiteSpace(value)
        ? value : throw new ArgumentException("Entity and field names must not be empty.");
}