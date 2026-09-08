using System.Collections.Generic;

namespace DevelKit.MessageTemplates.Query.Base;

public class ConditionExpression
{
    public string AttributeName { get; set; } = null!;
    // public string EntityName { get; set; } = null!;
    public ConditionOperator Operator { get; set; }
    public List<object> Values { get; set; } = new();
}