using System.Data.Common;
using DevelKit.MessageTemplates.Query;
using DevelKit.MessageTemplates.Query.Base;

namespace DevelKit.MessageTemplates.SqlServer;

/// <summary>Выполняет сгенерированный SQL и возвращает первую строку с псевдонимами колонок.</summary>
/// <remarks>Соединение должно быть открыто. Соединение и транзакция принадлежат вызывающему коду.
/// Одновременные вызовы на одном соединении не поддерживаются. Для метаданных используйте
/// PreparingTemplateDataProvider поверх этого провайдера.</remarks>
public sealed class SqlServerTemplateDataProvider(
    DbConnection connection, string schema = "dbo", DbTransaction? transaction = null) : ITemplateDataProvider
{
    public async Task<IReadOnlyDictionary<string, object?>?> ReadAsync(QueryExpression query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sql = SqlServerQueryGenerator.Generate(query, schema);
        await using var command = sql.CreateCommand(connection);
        command.Transaction = transaction;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
            row.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
        return row;
    }
}