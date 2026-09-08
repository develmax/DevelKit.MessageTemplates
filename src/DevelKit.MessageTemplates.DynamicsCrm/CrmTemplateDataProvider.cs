using DevelKit.MessageTemplates.Query;
using Microsoft.Xrm.Sdk;
using Model = DevelKit.MessageTemplates.Query.Base;

namespace DevelKit.MessageTemplates.DynamicsCrm;

/// <summary>Читает значения через переданный сервис и его контекст доступа.</summary>
/// <remarks>Временем жизни сервиса управляет вызывающее приложение.</remarks>
public sealed class CrmTemplateDataProvider : ITemplateDataProvider
{
    private readonly IOrganizationService service;

    public CrmTemplateDataProvider(IOrganizationService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public Task<IReadOnlyDictionary<string, object?>?> ReadAsync(Model.QueryExpression query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // SDK CRM 2015 синхронный. Task нужен для общего интерфейса поставщиков,
        // но выполняющийся RetrieveMultiple нельзя прервать этим CancellationToken.
        var rows = service.RetrieveMultiple(CrmQueryTranslator.Translate(query));
        cancellationToken.ThrowIfCancellationRequested();
        if (rows.Entities.Count == 0)
            return Task.FromResult<IReadOnlyDictionary<string, object?>?>(null);
        var values = rows.Entities[0].Attributes.ToDictionary(p => p.Key, p => Unwrap(p.Value));
        return Task.FromResult<IReadOnlyDictionary<string, object?>?>(values);
    }

    // Сохраняем имена колонок с alias, раскрывая только значения.
    // EntityReference превращается в Guid. Чтобы получить отображаемое имя, нужно запросить поле по связи.
    // DateTime остаётся неизменным: перевод в зону получателя выполняет приложение.
    private static object? Unwrap(object? value) => value switch
    {
        AliasedValue alias => Unwrap(alias.Value),
        OptionSetValue option => option.Value,
        Money money => money.Value,
        EntityReference reference => reference.Id,
        _ => value
    };
}