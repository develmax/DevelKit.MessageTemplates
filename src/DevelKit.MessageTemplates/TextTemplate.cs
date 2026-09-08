using System.Collections.Generic;
using DevelKit.MessageTemplates.Parameters;

namespace DevelKit.MessageTemplates
{
    /// <summary>Исходный текст и его верхнеуровневые параметры в порядке появления.</summary>
    /// <remarks>Дерево допускает изменения, но во время форматирования его нужно сохранять неизменным.</remarks>
    public class TextTemplate
    {
        public TextTemplate(string? value)
        {
            Value = value;
        }

        public string? Value { get; }

        public List<TextTemplateParameter> Parameters { get; } = new();
    }
}