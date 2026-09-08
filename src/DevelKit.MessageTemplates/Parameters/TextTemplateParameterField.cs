namespace DevelKit.MessageTemplates.Parameters
{
    public class TextTemplateParameterField
    {
        public int StartIndex { get; set; }
        public int Length { get; set; }

        public TextTemplateParameterOperatorIf? OperatorIf { get; set; }
        public string Name { get; set; } = null!;
        public string? Format { get; set; }
        public TextTemplateParameter? ExtendedFormat { get; set; }
    }
}