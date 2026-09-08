# Code guide

[Русский](../code-guide.md) | **English**

Start with the [database-free sample](../../samples/Meetings/Program.cs) or the [CRM SDK sample](../../samples/CrmMeetings/Program.cs). Then open TemplateEngine.RenderAsync to follow the complete rendering pipeline.

## Main path

1. TemplateAliases.Expand expands catalog keys into technical expressions in a single pass.
2. TextTemplateParser.Parse builds a tree and retains parameter positions in the original string. ParseWithRevert restores the cursor after an unsuccessful parsing attempt.
3. TextTemplateValidator.Validate checks recognized nodes. Unrecognized syntax may remain literal text.
4. TextTemplateQueryBuilder.Build merges columns and matching relationship paths. TemplateMapping maps each parameter occurrence to a result column.
5. ITemplateDataProvider.ReadAsync returns a single row for the plan. Root fields use field keys, while related fields use alias.field.
6. TextTemplateFormatter.Format copies text between parameters, evaluates conditions, and formats the loaded values.

## Details worth checking

- An if node has three children in a fixed order: the condition field, the true branch, and the false branch. A relationship node has one nested parameter.
- Relationship comparison includes filters. Otherwise, different participant roles could be merged into the same join.
- Both if branches are loaded in advance. The condition selects the output value. The application controls read permissions.
- The formatter removes preceding whitespace when a value is null. It does not fix punctuation or HTML-encode values.
- CachedTemplateAliases records the invalidation generation before loading so that invalidation during a read is not lost.
- CrmQueryTranslator creates actual SDK types. CrmTemplateDataProvider unwraps SDK values. The CRM 2015 call itself is synchronous.
- The SQL generator keeps relationship filters in ON clauses and passes values through DbParameter. Validating a column name does not authorize access to it.

Comments near the implementation explain these choices. Core public contracts have XML documentation, which is generated with the assemblies and included in packages for IDE help. Source comments and XML documentation are currently in Russian.
