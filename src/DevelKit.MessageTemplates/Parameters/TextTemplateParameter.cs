using System.Collections.Generic;

namespace DevelKit.MessageTemplates.Parameters
{
    /// <summary>Одно вхождение поля, перехода по связи или условия в тексте.</summary>
    /// <remarks>Повторяющиеся поля имеют отдельные узлы, но могут читать одну колонку результата.</remarks>
    public class TextTemplateParameter
    {
        public TextTemplateParameter(TextTemplate textTemplate, int startIndex, int length)
        {
            TextTemplate = textTemplate;
            StartIndex = startIndex;
            Length = length;
        }

        public string? EntityName { get; set; }

        public string? FieldName { get; set; }
        public string? Format { get; set; }

        // ReSharper disable once MemberCanBePrivate.Global
        public TextTemplate TextTemplate { get; set; }

        public int StartIndex { get; set; }
        public int Length { get; set; }

        public TextTemplateParameterType Type { get; set; }
        public TextTemplateParameterOperator? Operator { get; set; }

        public TextTemplateParameterConditionOperator? ConditionOperator { get; set; }

        public object? ConditionValue { get; set; }

        /// <summary>
        /// Для связи — вложенный параметр. Для if — проверяемое поле, истинная и ложная ветви.
        /// </summary>
        public List<TextTemplateParameter>? SubParameters { get; set; }

        public bool HasSubParameters => SubParameters != null && SubParameters.Count > 0;

        public TextTemplateParameter? Parent { get; set; }

        private TextTemplateParameter GetLastParent()
        {
            var templateParameter = this;
            while (templateParameter.Parent != null)
            {
                templateParameter = templateParameter.Parent;
            }

            return templateParameter;
        }

        public override string ToString()
        {
            if (Parent != null)
            {
                var parent = GetLastParent();
                var parentStart = parent.TextTemplate.Value!.Substring(parent.StartIndex, StartIndex - parent.StartIndex);
                var parentEnd = parent.TextTemplate.Value.Substring(StartIndex + Length, parent.Length - (StartIndex - parent.StartIndex) - Length);

                return parentStart + "<" + TextTemplate.Value!.Substring(StartIndex, Length) + ">" + parentEnd;
            }

            return TextTemplate.Value!.Substring(StartIndex, Length);
        }
    }
}