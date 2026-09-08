// ReSharper disable PropertyCanBeMadeInitOnly.Global
namespace DevelKit.MessageTemplates.Parameters
{
    public class TextTemplateParameterCondition
    {
        public TextTemplateParameterField Field { get; set; } = null!;
        public TextTemplateParameterConditionOperator ConditionOperator { get; set; }
        public object? Value { get; set; }
    }
}