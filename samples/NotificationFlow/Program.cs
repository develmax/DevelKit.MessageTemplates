using System.Data;
using DevelKit.MessageTemplates;
using DevelKit.MessageTemplates.Query;
using DevelKit.MessageTemplates.Query.Base;
using DevelKit.MessageTemplates.SqlServer;
using Microsoft.Data.SqlClient;

// По умолчанию используем локальную учебную среду. Строку подключения не выводим.
var connectionString = Environment.GetEnvironmentVariable("DEVELKIT_SAMPLE_SQL")
    ?? @"Server=(localdb)\MSSQLLocalDB;Database=tempdb;Integrated Security=true;Encrypt=false";
await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();
await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
// У каждого запуска своя схема. Все созданные объекты удалит откат транзакции.
var storageSchema = "notification_demo_" + Guid.NewGuid().ToString("N");
try
{
    var catalog = new NotificationCatalog(connection, transaction, storageSchema);
    await catalog.InitializeAsync();

    // Администратор сохраняет определения параметров отдельно от текстов сообщений.
    await catalog.AddParameterAsync("{{Клиент:Имя}}", "{{person:name}}");
    await catalog.AddParameterAsync("{{Встреча:Дата}}", "{{person:meetingAt(dd.MM.yyyy HH:mm)}}");
    // Автор сообщения использует только понятные названия из каталога.
    await catalog.AddTemplateAsync("meeting-reminder",
        "Здравствуйте, {{Клиент:Имя}}! Ждём вас {{Встреча:Дата}}.");

    // Два клиента нужны, чтобы показать выбор реальной записи по идентификатору.
    await catalog.AddPersonAsync(42, "Анна", new DateTime(2026, 9, 15, 11, 30, 0), 180, 1);
    await catalog.AddPersonAsync(43, "Борис", new DateTime(2026, 9, 16, 5, 0, 0), 300, 0);
    var schema = new TemplateSchema().AddEntity("person", "id", "name", "meetingAt", "utcOffset", "status");
    var source = new SqlServerTemplateDataProvider(connection, storageSchema, transaction);
    var provider = new PreparingTemplateDataProvider(source, catalog, schema);
    var engine = new TemplateEngine(schema, provider);
    var notifications = new NotificationService(catalog, engine);

    // Процесс уведомления знает код шаблона и клиента. Остальное загружается из SQL.
    var anna = await notifications.RenderAsync("meeting-reminder", 42);
    var boris = await notifications.RenderAsync("meeting-reminder", 43);
    Console.WriteLine(anna);
    Console.WriteLine(boris);
    Expect(anna, "Здравствуйте, Анна! Ждём вас 15.09.2026 14:30.");
    Expect(boris, "Здравствуйте, Борис! Ждём вас 16.09.2026 10:00.");

    // SQL bigint становится Int32 по метаданным до сравнения с литералом 1.
    await catalog.AddTemplateAsync("conditional", "{{person:if[[status = 1 ? name ; meetingAt(dd.MM.yyyy HH:mm)]]}}");
    Expect(await notifications.RenderAsync("conditional", 42), "Анна");
    Expect(await notifications.RenderAsync("conditional", 43), "16.09.2026 10:00");

    // Значение, похожее на SQL, должно оставаться данными клиента.
    await catalog.AddPersonAsync(44, "O'Brien; SELECT 1--", new DateTime(2026, 9, 17, 9, 0, 0), 0, 1);
    Expect(await notifications.RenderAsync("meeting-reminder", 44),
        "Здравствуйте, O'Brien; SELECT 1--! Ждём вас 17.09.2026 09:00.");
    try
    {
        await notifications.RenderAsync("missing-template", 42);
        throw new Exception("Missing template was not detected.");
    }
    catch (KeyNotFoundException) { }
    try
    {
        await notifications.RenderAsync("meeting-reminder", 999);
        throw new Exception("Missing person was not detected.");
    }
    catch (InvalidOperationException error) when (error.Message.StartsWith("No data for", StringComparison.Ordinal)) { }
    Console.WriteLine("Verified: two clients, UTC offsets, metadata types, both if branches, literal SQL-like text, missing template and missing client.");
}
finally
{
    await transaction.RollbackAsync();
    Console.WriteLine("Demo transaction rolled back. No sample tables or data retained.");
}

static void Expect(string actual, string expected)
{
    if (actual != expected) throw new Exception($"Unexpected message: {actual}");
}

// Этот прикладной сервис соединяет каталог сообщений с библиотекой шаблонизации.
sealed class NotificationService(NotificationCatalog catalog, TemplateEngine engine)
{
    public async Task<string> RenderAsync(string templateCode, int personId,
        CancellationToken cancellationToken = default)
    {
        var text = await catalog.FindTemplateAsync(templateCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Template not found: {templateCode}");
        var parameters = await catalog.LoadParametersAsync(cancellationToken);
        return await engine.RenderAsync(text,
            new Dictionary<string, object> { ["person"] = personId }, parameters,
            cancellationToken: cancellationToken);
    }
}

// Каталог принадлежит приложению. Движку не нужно знать, как сохраняют и ищут шаблоны.
sealed class NotificationCatalog(SqlConnection connection, SqlTransaction transaction, string schema) : ITemplateValueMetadataProvider
{
    public async Task InitializeAsync()
    {
        // Имя схемы генерирует программа. Пользовательский ввод в DDL не попадает.
        await ExecuteAsync($"CREATE SCHEMA [{schema}]");
        await ExecuteAsync($"""
            CREATE TABLE [{schema}].[parameters] (
                [name] nvarchar(100) NOT NULL PRIMARY KEY, [expansion] nvarchar(1000) NOT NULL);
            CREATE TABLE [{schema}].[templates] (
                [code] nvarchar(100) NOT NULL PRIMARY KEY, [body] nvarchar(max) NOT NULL);
            CREATE TABLE [{schema}].[person] (
                [id] int NOT NULL PRIMARY KEY, [name] nvarchar(200) NOT NULL, [meetingAt] datetime2 NOT NULL,
                [utcOffset] int NULL, [status] bigint NOT NULL);
            CREATE TABLE [{schema}].[fieldMetadata] (
                [entity] nvarchar(100) NOT NULL, [field] nvarchar(100) NOT NULL,
                [kind] nvarchar(30) NOT NULL, [offsetColumn] nvarchar(100) NULL,
                PRIMARY KEY ([entity], [field]));
            INSERT INTO [{schema}].[fieldMetadata] VALUES
                ('person', 'name', 'String', NULL),
                ('person', 'meetingAt', 'UtcDateTime', 'utcOffset'),
                ('person', 'status', 'Int32', NULL);
            """);
    }

    public Task AddParameterAsync(string name, string expansion) => ExecuteAsync(
        $"INSERT INTO [{schema}].[parameters] VALUES (@name, @expansion)",
        new SqlParameter("@name", SqlDbType.NVarChar, 100) { Value = name },
        new SqlParameter("@expansion", SqlDbType.NVarChar, 1000) { Value = expansion });

    public Task AddTemplateAsync(string code, string body) => ExecuteAsync(
        $"INSERT INTO [{schema}].[templates] VALUES (@code, @body)",
        new SqlParameter("@code", SqlDbType.NVarChar, 100) { Value = code },
        new SqlParameter("@body", SqlDbType.NVarChar, -1) { Value = body });

    public Task AddPersonAsync(int id, string name, DateTime meetingAt, int utcOffset, long status) => ExecuteAsync(
        $"INSERT INTO [{schema}].[person] VALUES (@id, @name, @meetingAt, @offset, @status)",
        new SqlParameter("@id", SqlDbType.Int) { Value = id },
        new SqlParameter("@name", SqlDbType.NVarChar, 200) { Value = name },
        new SqlParameter("@meetingAt", SqlDbType.DateTime2) { Value = meetingAt },
        new SqlParameter("@offset", SqlDbType.Int) { Value = utcOffset },
        new SqlParameter("@status", SqlDbType.BigInt) { Value = status });

    public async Task<string?> FindTemplateAsync(string code, CancellationToken cancellationToken)
    {
        await using var command = Command($"SELECT [body] FROM [{schema}].[templates] WHERE [code] = @code");
        command.Parameters.Add(new SqlParameter("@code", SqlDbType.NVarChar, 100) { Value = code });
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TemplateAlias>> LoadParametersAsync(CancellationToken cancellationToken)
    {
        await using var command = Command($"SELECT [name], [expansion] FROM [{schema}].[parameters] ORDER BY [name]");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var parameters = new List<TemplateAlias>();
        while (await reader.ReadAsync(cancellationToken))
            parameters.Add(new TemplateAlias(reader.GetString(0), reader.GetString(1)));
        return parameters;
    }

    // Метаданные хранятся отдельно от текстов. Для простоты пример читает весь каталог.
    // В большом приложении нужен выбор запрошенных полей и кеш с явной инвалидацией.
    public async Task<IReadOnlyDictionary<string, TemplateValueMetadata>> LoadAsync(
        IReadOnlyList<TemplateResultField> fields, CancellationToken cancellationToken)
    {
        await using var command = Command($"SELECT [entity], [field], [kind], [offsetColumn] FROM [{schema}].[fieldMetadata]");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var definitions = new Dictionary<(string Entity, string Field), TemplateValueMetadata>();
        while (await reader.ReadAsync(cancellationToken))
            definitions.Add((reader.GetString(0), reader.GetString(1)),
                new(Enum.Parse<TemplateValueKind>(reader.GetString(2)),
                    OffsetColumn: reader.IsDBNull(3) ? null : reader.GetString(3)));
        return fields.ToDictionary(f => f.Column, f => definitions[(f.Entity, f.Field)]);
    }

    private SqlCommand Command(string text) => new(text, connection, transaction);

    private async Task ExecuteAsync(string text, params SqlParameter[] parameters)
    {
        await using var command = Command(text);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }
}
