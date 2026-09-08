using System.Collections.Generic;

// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable MemberCanBePrivate.Global
namespace DevelKit.MessageTemplates.Query.Base;

/// <summary>Узел соединения с выбранными колонками, фильтром и дочерними переходами.</summary>
public class LinkEntity
{
    public ColumnSet Columns { get; set; } = new();
    public string? EntityAlias { get; set; }
    public JoinOperator JoinOperator { get; set; }
    public FilterExpression LinkCriteria { get; set; } = new();
    public List<LinkEntity> LinkEntities { get; set; } = new();
    public string LinkFromAttributeName { get; set; } = null!;
    public string? LinkFromEntityName { get; set; }
    public string LinkToAttributeName { get; set; } = null!;
    public string LinkToEntityName { get; set; } = null!;

    public LinkEntity AddLink(string linkToEntityName, string linkFromAttributeName, string linkToAttributeName, JoinOperator joinOperator)
    {
        var link = new LinkEntity
        {
            LinkFromEntityName = LinkFromEntityName,
            LinkToEntityName = linkToEntityName,
            LinkFromAttributeName = linkFromAttributeName,
            LinkToAttributeName = linkToAttributeName,
            JoinOperator = joinOperator
        };

        LinkEntities.Add(link);

        return link;
    }
}