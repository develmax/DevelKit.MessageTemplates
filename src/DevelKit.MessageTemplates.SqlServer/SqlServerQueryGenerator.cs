using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using DevelKit.MessageTemplates.Query.Base;

namespace DevelKit.MessageTemplates.SqlServer;

/// <summary>Текст команды и значения параметров в порядке @p0, @p1 и далее.</summary>
public sealed record SqlQuery(string Text, IReadOnlyList<object> Parameters)
{
    /// <summary>Создаёт команду на переданном соединении. Открытием соединения и выполнением команды управляет вызывающий код.</summary>
    /// <remarks>Командой, соединением, транзакцией и специфичными типами параметров управляет вызывающий код.</remarks>
    public DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = Text;
        for (var i = 0; i < Parameters.Count; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"@p{i}";
            parameter.Value = Parameters[i] ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }
}

/// <summary>Преобразует план в диалект SQL Server, не изменяя дерево и не выполняя запрос.</summary>
public static class SqlServerQueryGenerator
{
    public static SqlQuery Generate(QueryExpression query, string schema = "dbo")
    {
        var fields = new List<string>();
        var joins = new StringBuilder();
        var values = new List<object>();
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "r" };
        foreach (var column in query.ColumnSet.Columns.Distinct())
            fields.Add($"[r].{Id(column)} AS {Id(column)}");
        Visit(query.LinkEntities, "r");
        if (fields.Count == 0) throw new InvalidOperationException("Query has no selected fields.");
        var top = query.TopCount is > 0 ? $"TOP {query.TopCount.Value} " : "";
        var text = $"SELECT {top}{string.Join(", ", fields)} FROM {Id(schema)}.{Id(query.EntityName)} AS [r]";
        if (query.NoLock) text += " WITH(NOLOCK)";
        text += joins;
        if (query.Criteria.Conditions.Count > 0) text += " WHERE " + Conditions(query.Criteria, "r");
        return new(text, values);

        void Visit(IEnumerable<LinkEntity> links, string parent)
        {
            foreach (var link in links)
            {
                var alias = link.EntityAlias ?? throw new InvalidOperationException("Join alias is required.");
                if (!aliases.Add(alias)) throw new InvalidOperationException($"Duplicate alias: {alias}");
                foreach (var column in link.Columns.Columns.Distinct())
                    fields.Add($"{Id(alias)}.{Id(column)} AS [{alias}.{Safe(column)}]");
                var join = link.JoinOperator switch
                {
                    JoinOperator.Inner => "INNER JOIN",
                    JoinOperator.LeftOuter => "LEFT OUTER JOIN",
                    _ => throw new NotSupportedException("Join operator")
                };
                joins.Append($" {join} {Id(schema)}.{Id(link.LinkToEntityName)} AS {Id(alias)}");
                if (query.NoLock) joins.Append(" WITH(NOLOCK)");
                joins.Append($" ON {Id(parent)}.{Id(link.LinkFromAttributeName)} = {Id(alias)}.{Id(link.LinkToAttributeName)}");
                // Фильтр связи остаётся в ON: перенос в WHERE изменил бы смысл LEFT JOIN.
                if (link.LinkCriteria.Conditions.Count > 0)
                    joins.Append($" AND ({Conditions(link.LinkCriteria, alias)})");
                Visit(link.LinkEntities, alias);
            }
        }

        string Conditions(FilterExpression filter, string alias)
        {
            var parts = new List<string>();
            foreach (var condition in filter.Conditions)
            {
                if (condition.Operator != ConditionOperator.Equal || condition.Values.Count != 1)
                    throw new NotSupportedException("Only equality with one value is supported.");
                var column = $"{Id(alias)}.{Id(condition.AttributeName)}";
                if (condition.Values[0] is null) parts.Add($"{column} IS NULL");
                else
                {
                    // Значения не входят в SQL-текст: CreateCommand создаст соответствующие DbParameter.
                    parts.Add($"{column} = @p{values.Count}");
                    values.Add(condition.Values[0]);
                }
            }
            var separator = filter.FilterOperator switch
            {
                LogicalOperator.And => " AND ",
                LogicalOperator.Or => " OR ",
                _ => throw new NotSupportedException("Logical operator")
            };
            return string.Join(separator, parts);
        }
    }

    // Имена таблиц/колонок нельзя передать DbParameter. Ограничиваем их синтаксис отдельно.
    // Разрешение читать конкретное поле проверяется схемой приложения, а не этим regex.
    private static string Id(string name) => $"[{Safe(name)}]";
    private static string Safe(string name) =>
        Regex.IsMatch(name, @"\A[\p{L}_][\p{L}\p{Nd}_]*\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            ? name : throw new ArgumentException($"Invalid SQL identifier: {name}");
}