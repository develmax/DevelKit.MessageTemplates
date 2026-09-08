using System.Collections.Generic;
using System.Linq;

// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace DevelKit.MessageTemplates.Query.Base;

public class FilterExpression
{
    public List<ConditionExpression> Conditions { get; set; } = new();
    public LogicalOperator FilterOperator { get; set; }
    // public List<FilterExpression> Filters { get; set; } = new();

    public void AddCondition(string attributeName, ConditionOperator @operator, object[] value)
    {
        var condition = new ConditionExpression
        {
            AttributeName = attributeName,
            Operator = @operator,
            Values = value.ToList()
        };

        Conditions.Add(condition);
    }

    public void AddCondition(string attributeName, ConditionOperator @operator, object value)
    {
        var condition = new ConditionExpression
        {
            AttributeName = attributeName,
            Operator = @operator,
            Values = new List<object>
                                     {
                                         value
                                     }
        };

        Conditions.Add(condition);
    }
}