namespace DevelKit.MessageTemplates.Query.Base;

public class AliasedValue
{
    public AliasedValue(object value)
    {
        Value = value;
    }

    // public string AttributeLogicalName { get; set; } = null!;
    // public string EntityLogicalName { get; set; } = null!;
    public object? Value { get; set; }
}