using System.Collections.Generic;

namespace DevelKit.MessageTemplates.Query.Base;

/// <summary>Независимый план чтения. Локальная модель сохраняет привычную структуру QueryExpression из CRM SDK.</summary>
public class QueryExpression
{
    public QueryExpression(string entityName)
    {
        EntityName = entityName;
    }

    public ColumnSet ColumnSet { get; set; } = new ColumnSet();
    public FilterExpression Criteria { get; set; } = new();
    // public bool Distinct { get; set; }
    public string EntityName { get; set; }
    public List<LinkEntity> LinkEntities { get; set; } = new();
    public bool NoLock { get; set; }
    // public List<OrderExpression> Orders { get; set; } = new();
    // public PagingInfo PageInfo { get; set; }
    public int? TopCount { get; set; }
}