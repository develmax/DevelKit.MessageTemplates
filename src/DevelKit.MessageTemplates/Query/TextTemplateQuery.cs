using System.Collections.Generic;
using DevelKit.MessageTemplates.Parameters;
using DevelKit.MessageTemplates.Query.Base;

namespace DevelKit.MessageTemplates.Query
{
    /// <summary>Запросы к корневым сущностям и соответствие каждого узла колонке результата.</summary>
    public class TextTemplateQuery
    {
        public List<QueryExpression> SubQueries { get; } = new();
        public Dictionary<TextTemplateParameter, KeyValuePair<QueryExpression, string>> TemplateMapping { get; } = new();
    }
}