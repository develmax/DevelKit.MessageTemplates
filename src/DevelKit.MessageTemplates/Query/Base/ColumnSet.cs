using System.Collections.Generic;

namespace DevelKit.MessageTemplates.Query.Base;

public class ColumnSet
{
    public ColumnSet(params string[] values)
    {
        Columns.UnionWith(values);
    }

    public HashSet<string> Columns { get; set; } = new();
}