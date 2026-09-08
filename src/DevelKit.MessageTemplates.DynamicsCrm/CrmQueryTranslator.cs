using Model = DevelKit.MessageTemplates.Query.Base;
using SdkQuery = Microsoft.Xrm.Sdk.Query;

namespace DevelKit.MessageTemplates.DynamicsCrm;

/// <summary>Переводит независимый план в типы CRM SDK.</summary>
public static class CrmQueryTranslator
{
    public static SdkQuery.QueryExpression Translate(Model.QueryExpression plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        var query = new SdkQuery.QueryExpression(plan.EntityName)
        {
            ColumnSet = new SdkQuery.ColumnSet(plan.ColumnSet.Columns.ToArray()),
            Criteria = TranslateFilter(plan.Criteria),
            NoLock = plan.NoLock,
            TopCount = plan.TopCount
        };
        foreach (var link in plan.LinkEntities) query.LinkEntities.Add(TranslateLink(link, plan.EntityName));
        return query;
    }

    private static SdkQuery.LinkEntity TranslateLink(Model.LinkEntity link, string parentEntity)
    {
        // Родитель берётся из обхода дерева: у вложенной связи это предыдущая целевая сущность.
        var result = new SdkQuery.LinkEntity(parentEntity, link.LinkToEntityName,
            link.LinkFromAttributeName, link.LinkToAttributeName, link.JoinOperator switch
            {
                Model.JoinOperator.Inner => SdkQuery.JoinOperator.Inner,
                Model.JoinOperator.LeftOuter => SdkQuery.JoinOperator.LeftOuter,
                _ => throw new NotSupportedException("Unsupported join operator.")
            })
        {
            EntityAlias = link.EntityAlias,
            Columns = new SdkQuery.ColumnSet(link.Columns.Columns.ToArray()),
            LinkCriteria = TranslateFilter(link.LinkCriteria)
        };
        foreach (var child in link.LinkEntities)
            result.LinkEntities.Add(TranslateLink(child, link.LinkToEntityName));
        return result;
    }

    private static SdkQuery.FilterExpression TranslateFilter(Model.FilterExpression filter)
    {
        var result = new SdkQuery.FilterExpression(filter.FilterOperator switch
        {
            Model.LogicalOperator.And => SdkQuery.LogicalOperator.And,
            Model.LogicalOperator.Or => SdkQuery.LogicalOperator.Or,
            _ => throw new NotSupportedException("Unsupported logical operator.")
        });
        foreach (var condition in filter.Conditions)
        {
            if (condition.Operator != Model.ConditionOperator.Equal || condition.Values.Count != 1)
                throw new NotSupportedException("Only equality with one value is supported.");
            var value = condition.Values[0];
            // В SDK отсутствие значения выражается оператором Null, а не Equal(null).
            if (value == null) result.AddCondition(condition.AttributeName, SdkQuery.ConditionOperator.Null);
            else result.AddCondition(condition.AttributeName, SdkQuery.ConditionOperator.Equal, value);
        }
        return result;
    }
}