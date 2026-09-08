# Behavior and compatibility

[Русский](../compatibility.md) | **English**

## Core behavior

- Syntax: {{entity:field}}, {{entity:field(format)}}, and {{entity:relation{{other:field}}}}.
- Conditions: if[[field operator literal ? field ; field]], including nested branches and relationships.
- Operators: =, !=, <, >, <=, >=. Literals: strings, nonnegative Int32 values, true, false, and null.
- Nodes retain source positions, Parent references, and SubParameters.
- Unrecognized constructs usually remain literal text. The validator checks the tree rather than verifying that the entire source was parsed.
- A null value removes all preceding whitespace from the output buffer, including line breaks. Following whitespace is retained.
- Equality uses CLR Equals: an Int32 and an Int64 with the same numeric value are not equal. Ordered comparisons require compatible numeric types.
- Duplicate aliases use the last definition. Lookup is case-insensitive and expansion takes one pass. An inactive alias is replaced with an empty string.
- Shared relationships and columns are merged. Individual text nodes retain separate mappings.
- Both branches of a condition are included in the query plan. The formatter selects one branch.
- Plans return one row, without aggregation or collections.

## Current implementation

- The core namespace is DevelKit.MessageTemplates.
- Both the parser and formatter accept null text and produce an empty result.
- Field parsing depth is limited to 64. Exceeding it throws FormatException.
- IQueryMetadata defines entities and relationships, including chains through intermediate tables with filters.
- Root identifiers use object and are not restricted to Guid.
- A missing root target or result row causes InvalidOperationException.
- ITemplateDataProvider returns plain CLR values without requiring source-specific wrappers.
- Normalization and time zone handling belong to the application or its transform callback.
- The SQL Server generator has no fixed database name, does not mutate the query tree, qualifies filters with aliases, and passes values through DbParameter.
- NOLOCK is off by default. The SQL generator adds it when QueryExpression.NoLock is explicitly enabled.
- The cache uses SemaphoreSlim, a Func<DateTimeOffset> clock, configurable TTL, and an invalidation generation. It needs no external locking library.
- Invalid alias definition names are rejected as a whole.

## Limitations

- The core targets net452 and net10.0. The CRM adapter targets net452 with SDK 7.0.0.1. The SQL adapter targets net10.0. CRM scenarios have been tested on a compatible .NET Framework using a fake IOrganizationService, without a live CRM instance.
- Formatting uses CurrentCulture. Time zones are not detected automatically.
- There is no HTML encoding. The application must choose encoding appropriate to the insertion context. The library is a text renderer, not a complete safe HTML templating engine.
- There is no strict syntax mode. Large numeric literals may throw OverflowException. Nesting is limited, but no overall input-length or parsing-time limit is configured.
- Doubled single quotes in string literals remain doubled. Negative and fractional numeric literals are unsupported.
- The SQL generator supports SQL Server, equality filters, AND/OR, and INNER/LEFT JOIN. It is not a full ORM or a compatible clone of the CRM QueryExpression API.
- TOP 1 without ORDER BY does not select a stable row from a one-to-many relationship. Application metadata must ensure an unambiguous result.
- A dedicated CRM SDK provider is included. SQL execution remains in the application. The library includes no template editor, REST host, message delivery, or SMS/email channel rules.
- Public tree models are mutable. Do not change a tree during rendering or share a mutable plan across concurrent operations.
- Metadata must restrict fields and relationships. SQL identifier validation does not replace access control.

## Cache and CRM adapter

CachedTemplateAliases accepts a clock delegate for controlled time. TemplateSchema defines an explicit list of allowed entities, fields, and relationships. Complete configuration before concurrent use.

The CRM adapter converts the neutral plan into SDK QueryExpression and unwraps AliasedValue, OptionSetValue, Money, and EntityReference into CLR values. RetrieveMultiple is synchronous: cancellation is checked before and after the call but cannot interrupt an active request. The adapter does not own the supplied service. Deployment into CRM and compatibility with modern Dataverse have not been verified.
