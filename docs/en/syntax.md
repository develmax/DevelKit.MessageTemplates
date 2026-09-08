# Syntax and processing

[Русский](../syntax.md) | **English**

## Parameters

```text
{{person:name}}
{{meeting:startsAt(dd.MM.yyyy HH:mm)}}
{{meeting:guest{{person:name}}}}
{{meeting:if[[isOnline = true ? meetingUrl ; address]]}}
```

A parameter starts with an entity name. A field or if expression follows the colon. A nested parameter describes a relationship traversal. Parentheses specify a .NET format for the final field value.

if compares a field value with a literal and selects one of two branches. Branches contain fields, relationship paths, or further conditions. They do not accept arbitrary C# or string constants in place of a field. Supported comparisons are =, !=, <, >, <=, and >=. Additional operators, arithmetic, loops, and method calls are not supported.

## EBNF grammar

::= defines a production, | separates alternatives, ? marks an optional part, * permits zero or more repetitions, and + requires one or more. Quoted strings are literal terminals.

```ebnf
Parameter ::= "{{" S Name S ":" S Field S "}}"
Field     ::= If | Name S (Parameter | Format)?
If        ::= "if" S "[[" S Condition S "?" S Field S ";" S Field S "]]"
Condition ::= Field S Operator S Value S
Operator  ::= "!=" | "<=" | ">=" | "=" | ">" | "<"
Value     ::= String | Integer | Boolean | Null
Boolean   ::= "true" | "false"
Null      ::= "null"
Name      ::= NameChar+
Integer   ::= Digit+
String    ::= "'" (StringChar | "''")* "'"
Format    ::= "(" FormatChar+ ")"
S         ::= (#x20 | #x9 | #xD | #xA)*
```

NameChar matches char.IsLetter, char.IsNumber, or an underscore. Digit matches char.IsDigit. StringChar accepts any character except a single quote. FormatChar accepts any character except an opening or closing parenthesis. S contains spaces, tabs, and line breaks. Names are also checked against the allowed schema.

Implementation details:

- Names may start with digits. Keywords are case-sensitive.
- Numbers are converted through int.Parse. Negative and fractional values are unsupported. Overflow throws an exception.
- Empty strings are allowed. Doubled single quotes inside strings remain doubled.
- Empty formats and parentheses inside formats are unsupported.
- if is tried before a plain name. Longer operators are tried before shorter ones.
- The parser accepts Field on the left side of a comparison, but the formatter does not evaluate a nested if in that position. Use a field or relationship path there. Nested conditions in branches are evaluated recursively.
- Parsing depth is limited to 64.

This grammar describes a recognized parameter. The input is scanned from left to right. After an unsuccessful parsing attempt, the cursor is restored and scanning continues. Unrecognized parts remain text. TextTemplateValidator checks the resulting tree and is not a strict validator of the original string.

## Named parameter catalog

TemplateAliases.Expand replaces short keys such as {{meeting:date}} with full catalog expressions. Keys use {{entity:field}} syntax, with both names composed of regex \w characters. Whitespace is allowed around each part, but not inside either name. Applications can store user-facing labels separately.

Lookup is case-insensitive. The last definition of a name wins, and inactive definitions expand to an empty string. Expansion takes one pass: inserted definitions are not expanded through the catalog again.

CachedTemplateAliases loads definitions through ITemplateAliasRepository and supports TTL and Invalidate. Reuse the cache instance. Call ExpandAsync before passing the result to TemplateEngine.

## From template to message

1. Expand the catalog.
2. Parse parameters and validate the tree.
3. Build a query plan using application metadata.
4. Load one row through ITemplateDataProvider.
5. Select branches and format values.

Both if branches are included in the query plan. The condition is neither a SQL WHERE clause nor an access-control rule.

[Limitations and null handling](compatibility.md).
