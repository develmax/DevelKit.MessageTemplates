namespace DevelKit.MessageTemplates.Parameters
{
    public class TextTemplateParameterOperatorIf
    {
        public TextTemplateParameterCondition Condition { get; set; } = null!;
        public TextTemplateParameterField Field1 { get; set; } = null!;
        public TextTemplateParameterField Field2 { get; set; } = null!;
    }
}