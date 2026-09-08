using DevelKit.MessageTemplates.Query;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace DevelKit.MessageTemplates.DynamicsCrm;

/// <summary>Получает типы атрибутов через CRM SDK и переводит их в общие правила подготовки.</summary>
/// <remarks>configure задаёт прикладные правила пустых значений и времени. Метаданные сами по себе
/// не предоставляют права чтения. Схема и контекст IOrganizationService остаются обязательными.
/// В пределах вызова повторные поля запрашиваются один раз. Долговременный кеш задаёт приложение.</remarks>
public sealed class CrmTemplateValueMetadataProvider(
    IOrganizationService service,
    Func<TemplateResultField, TemplateValueMetadata, TemplateValueMetadata>? configure = null) : ITemplateValueMetadataProvider
{
    public Task<IReadOnlyDictionary<string, TemplateValueMetadata>> LoadAsync(
        IReadOnlyList<TemplateResultField> fields, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, TemplateValueMetadata>(StringComparer.Ordinal);
        var types = new Dictionary<string, TemplateValueMetadata>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = field.Entity + "." + field.Field;
            if (!types.TryGetValue(key, out var rule))
            {
                var response = (RetrieveAttributeResponse)service.Execute(new RetrieveAttributeRequest
                {
                    EntityLogicalName = field.Entity, LogicalName = field.Field, RetrieveAsIfPublished = false
                });
                rule = FromType(response.AttributeMetadata.AttributeType
                    ?? throw new InvalidOperationException($"No CRM attribute type: {key}."));
                // SDK 7.0 не содержит DateTimeBehavior. Исключения для конкретных полей задаёт configure.
                types.Add(key, rule);
            }
            result.Add(field.Column, configure is null ? rule : configure(field, rule));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyDictionary<string, TemplateValueMetadata>>(result);
    }

    private static TemplateValueMetadata FromType(AttributeTypeCode type) => new(type switch
    {
        AttributeTypeCode.Boolean => TemplateValueKind.Boolean,
        AttributeTypeCode.Integer or AttributeTypeCode.Picklist or AttributeTypeCode.State or AttributeTypeCode.Status => TemplateValueKind.Int32,
        AttributeTypeCode.BigInt => TemplateValueKind.Int64,
        AttributeTypeCode.Decimal or AttributeTypeCode.Money => TemplateValueKind.Decimal,
        AttributeTypeCode.Double => TemplateValueKind.Double,
        AttributeTypeCode.DateTime => TemplateValueKind.UtcDateTime,
        AttributeTypeCode.Customer or AttributeTypeCode.Lookup or AttributeTypeCode.Owner or AttributeTypeCode.Uniqueidentifier => TemplateValueKind.Guid,
        AttributeTypeCode.String or AttributeTypeCode.Memo => TemplateValueKind.String,
        _ => throw new NotSupportedException($"Unsupported CRM attribute type: {type}.")
    });
}
