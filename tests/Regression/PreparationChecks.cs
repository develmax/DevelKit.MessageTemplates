using DevelKit.MessageTemplates;
using DevelKit.MessageTemplates.Query;
using DevelKit.MessageTemplates.Query.Base;

static class PreparationChecks
{
    public static async Task RunAsync()
    {
        var checks = 0;
        void Equal(object? expected, object? actual)
        {
            if (!Equals(expected, actual)) throw new Exception($"Expected {expected}, actual {actual}");
            checks++;
        }
        void Fails<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { checks++; return; }
            throw new Exception("Expected " + typeof(T).Name);
        }
        var empty = new Dictionary<string, object?>();
        object? Convert(object? value, TemplateValueMetadata rule) => TemplateValueConverter.Convert(value, rule, empty);
        Equal(3, Convert(3L, new(TemplateValueKind.Int32)));
        Fails<OverflowException>(() => Convert(long.MaxValue, new(TemplateValueKind.Int32)));
        Fails<ArgumentException>(() => Convert(1.5m, new(TemplateValueKind.Int32)));
        Equal(true, Convert(1L, new(TemplateValueKind.Boolean)));
        Fails<ArgumentException>(() => Convert(2, new(TemplateValueKind.Boolean)));
        Equal(null, Convert(DBNull.Value, new(TemplateValueKind.String)));
        Equal(null, Convert("UNKNOWN", new(TemplateValueKind.String, NullSentinel: "unknown")));
        Equal("UNKNOWN", Convert("UNKNOWN", new(TemplateValueKind.String)));
        var date = new DateTime(2026, 9, 15, 11, 30, 0);
        Equal(DateTimeKind.Utc, ((DateTime)Convert(date, new(TemplateValueKind.UtcDateTime))!).Kind);
        Fails<ArgumentException>(() => Convert(DateTime.SpecifyKind(date, DateTimeKind.Local), new(TemplateValueKind.UtcDateTime)));
        var zone = TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(3), "test", "test");
        Equal(date.AddHours(3), Convert(date, new(TemplateValueKind.UtcDateTime, FallbackTimeZone: zone)));
        Fails<InvalidOperationException>(() => Convert(date, new(TemplateValueKind.UtcDateTime, OffsetColumn: "offset")));

        var schema = new TemplateSchema().AddEntity("person", "id", "date", "offset", "count", "yes", "no");
        var source = new CapturingSource(new Dictionary<string, object?>
        { ["date"] = date, ["offset"] = 180, ["count"] = 3L, ["yes"] = "yes", ["no"] = "no" });
        var metadata = new Rules(field => field.Field switch
        {
            "date" => new(TemplateValueKind.UtcDateTime, OffsetColumn: "offset"),
            "count" => new(TemplateValueKind.Int32),
            _ => new(TemplateValueKind.String)
        });
        var provider = new PreparingTemplateDataProvider(source, metadata, schema);
        var query = new QueryExpression("person") { ColumnSet = new("date") };
        var row = (await provider.ReadAsync(query, default))!;
        Equal(date.AddHours(3), row["date"]);
        Equal(DateTimeKind.Unspecified, ((DateTime)row["date"]!).Kind);
        Equal(true, source.Last!.ColumnSet.Columns.Contains("offset"));
        Equal(false, query.ColumnSet.Columns.Contains("offset"));
        Equal(false, row.ContainsKey("offset"));
        var engine = new TemplateEngine(schema, provider);
        Equal("yes", await engine.RenderAsync("{{person:if[[count = 3 ? yes ; no]]}}",
            new Dictionary<string, object> { ["person"] = 1 }));

        source.Values!["offset"] = null;
        row = (await provider.ReadAsync(query, default))!;
        Equal(DateTimeKind.Utc, ((DateTime)row["date"]!).Kind);
        source.Values["offset"] = 841;
        try { await provider.ReadAsync(query, default); throw new Exception("Expected invalid offset"); }
        catch (ArgumentOutOfRangeException) { checks++; }
        var before = source.Calls;
        var missing = new PreparingTemplateDataProvider(source, new EmptyRules(), schema);
        try { await missing.ReadAsync(query, default); throw new Exception("Expected missing metadata"); }
        catch (InvalidOperationException) { Equal(before, source.Calls); }
        var forbidden = new PreparingTemplateDataProvider(source,
            new Rules(_ => new(TemplateValueKind.UtcDateTime, OffsetColumn: "secret")), schema);
        try { await forbidden.ReadAsync(query, default); throw new Exception("Expected forbidden field"); }
        catch (ArgumentException) { Equal(before, source.Calls); }

        var linkedSchema = new TemplateSchema().AddEntity("root", "id").AddEntity("person", "id", "date", "offset");
        var linkedMetadata = new Rules(field =>
        {
            Equal("person", field.Entity);
            Equal("date", field.Field);
            Equal("t1.date", field.Column);
            return new(TemplateValueKind.UtcDateTime, OffsetColumn: "t1.offset");
        });
        var linkedSource = new CapturingSource(new Dictionary<string, object?>
            { ["t1.date"] = date, ["t1.offset"] = -60 });
        var linkedQuery = new QueryExpression("root");
        linkedQuery.LinkEntities.Add(new()
        {
            EntityAlias = "t1", LinkFromEntityName = "root", LinkFromAttributeName = "personId",
            LinkToEntityName = "person", LinkToAttributeName = "id", Columns = new("date")
        });
        var linked = new PreparingTemplateDataProvider(linkedSource, linkedMetadata, linkedSchema);
        var linkedRow = (await linked.ReadAsync(linkedQuery, default))!;
        Equal(date.AddHours(-1), linkedRow["t1.date"]);
        Equal(true, linkedSource.Last!.LinkEntities[0].Columns.Columns.Contains("offset"));
        Equal(false, linkedQuery.LinkEntities[0].Columns.Columns.Contains("offset"));

        source.Values = null;
        Equal(null, await provider.ReadAsync(query, default));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        before = source.Calls;
        try { await provider.ReadAsync(query, cts.Token); throw new Exception("Expected cancellation"); }
        catch (OperationCanceledException) { Equal(before, source.Calls); }
        Console.WriteLine($"Passed {checks} preparation assertions.");
    }

    sealed class CapturingSource(Dictionary<string, object?>? values) : ITemplateDataProvider
    {
        public Dictionary<string, object?>? Values = values;
        public QueryExpression? Last;
        public int Calls;
        public Task<IReadOnlyDictionary<string, object?>?> ReadAsync(QueryExpression query, CancellationToken token)
        {
            Calls++;
            Last = query;
            return Task.FromResult<IReadOnlyDictionary<string, object?>?>(Values);
        }
    }

    sealed class Rules(Func<TemplateResultField, TemplateValueMetadata> rule) : ITemplateValueMetadataProvider
    {
        public Task<IReadOnlyDictionary<string, TemplateValueMetadata>> LoadAsync(
            IReadOnlyList<TemplateResultField> fields, CancellationToken token) =>
            Task.FromResult<IReadOnlyDictionary<string, TemplateValueMetadata>>(fields.ToDictionary(f => f.Column, rule));
    }

    sealed class EmptyRules : ITemplateValueMetadataProvider
    {
        public Task<IReadOnlyDictionary<string, TemplateValueMetadata>> LoadAsync(
            IReadOnlyList<TemplateResultField> fields, CancellationToken token) =>
            Task.FromResult<IReadOnlyDictionary<string, TemplateValueMetadata>>(new Dictionary<string, TemplateValueMetadata>());
    }
}
