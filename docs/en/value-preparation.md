# Preparing values before formatting

[Русский](../value-preparation.md)

SqlServerQueryGenerator builds a query. SqlServerTemplateDataProvider executes it and returns the first row. Wrap it in PreparingTemplateDataProvider to load field metadata, include required time-offset columns, and convert values before evaluating conditions.

    var source = new SqlServerTemplateDataProvider(connection, "dbo", transaction);
    var provider = new PreparingTemplateDataProvider(source, valueMetadata, schema);
    var engine = new TemplateEngine(schema, provider);
    var text = await engine.RenderAsync(template, targets, aliases);

The application owns the open connection and transaction. Use one SQL provider sequentially. valueMetadata implements ITemplateValueMetadataProvider. schema defines allowed fields and relationships.

## Execution order

1. The engine expands aliases and builds a query plan.
2. The decorator calls LoadAsync with the source entity, field, and result column for each selected field.
3. Metadata must provide a rule for every column, including explicit Raw rules.
4. The decorator validates offset dependencies and adds columns to a copy of the query.
5. The SQL provider executes a parameterized command.
6. The decorator converts values and returns the originally requested columns.
7. The engine uses TemplateMapping to associate columns with template nodes. The formatter then evaluates conditions and applies formats.

Both branches of an if are queried and prepared before selecting the result. SQL Int64 declared as Int32 is converted before comparison with an integer literal. Overflow fails explicitly. The formatter's comparison semantics remain unchanged.

## Metadata rules

    new TemplateValueMetadata(TemplateValueKind.Int32);
    new TemplateValueMetadata(TemplateValueKind.String, NullSentinel: "UNKNOWN");
    new TemplateValueMetadata(TemplateValueKind.UtcDateTime,
        OffsetColumn: "utcOffset", FallbackTimeZone: applicationTimeZone);

NullSentinel is case-insensitive and scoped to one field. Null and DBNull remain null. Numeric strings are not implicitly parsed. References are represented as Guid. Request a related display name separately.

UtcDateTime explicitly interprets SQL DateTime values with unspecified kind as UTC. DateTimeOffset is converted to UTC. Local-kind dates are rejected because the source adapter must identify their original zone. Applying an offset returns DateTimeKind.Unspecified: recipient time is not server-local time.

Offsets are minutes in the range −840 to 840. A null offset uses FallbackTimeZone, or UTC if no fallback is configured. A missing result column is an error. Use TimeZoneInfo for seasonal transitions or supply an offset calculated for the event date.

OffsetColumn accepts a root column or alias.field on an existing join. The decorator adds columns but does not create missing relationships. If a time zone lives in another table, the application adapter must include the relationship in its plan or expose an offset through a source view. Priority between customer, lead, and organization time zones is an application policy.

Metadata comes from a trusted catalog and does not grant access. Schema validation and source permissions still apply. LoadAsync receives all fields of one query. The implementation owns caching and invalidation.

## Dynamics CRM

    var valueMetadata = new CrmTemplateValueMetadataProvider(service,
        (field, rule) => field.Field == "firstname"
            ? rule with { NullSentinel = "UNKNOWN" }
            : rule);
    var provider = new PreparingTemplateDataProvider(
        new CrmTemplateDataProvider(service), valueMetadata, schema);
    var engine = new TemplateEngine(schema, provider);

The CRM data provider unwraps AliasedValue, OptionSetValue, Money, and EntityReference. The metadata provider requests attribute types through RetrieveAttributeRequest, deduplicating repeated entity/field pairs within a call. Preparation happens after SDK unwrapping.

This adapter targets SDK 7.0 and .NET Framework 4.5.2. That SDK has no DateTimeBehavior. DateTime attributes default to UTC. Configure field-specific exceptions explicitly, for example with Raw. SDK regression checks use a test IOrganizationService, not a live CRM instance.

See [NotificationFlow](../../samples/NotificationFlow/Program.cs) for an executable SQL scenario with persisted aliases, templates, metadata, two customers, and output assertions. Transliteration, channel limits, and delivery remain application steps after rendering.
