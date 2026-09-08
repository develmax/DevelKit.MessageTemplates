using System.Collections.Generic;
using DevelKit.MessageTemplates.Parameters;

namespace DevelKit.MessageTemplates
{
    /// <summary>
    /// Проверяет обязательные поля уже распознанных узлов.
    /// Не проверяет неизвестные фрагменты исходного текста и права чтения сущностей.
    /// </summary>
    public static class TextTemplateValidator
    {
        public static bool Validate(TextTemplate template, out List<string> errors)
        {
            errors = new List<string>();

            foreach (var parameter in template.Parameters)
            {
                Validate(parameter, errors);
            }

            return errors.Count == 0;
        }

        private static void Validate(TextTemplateParameter parameter, List<string> errors)
        {
            if (string.IsNullOrEmpty(parameter.EntityName) && (parameter.Parent == null || parameter.Parent.Type != TextTemplateParameterType.Operator))
            {
                errors.Add($"EntityName is empty. Template is '{parameter}'.");
            }

            if (string.IsNullOrEmpty(parameter.FieldName) && (parameter.Type != TextTemplateParameterType.Operator))
            {
                errors.Add($"FieldName is empty. Template is '{parameter}'.");
            }

            if (parameter.SubParameters != null)
            {
                foreach (var p in parameter.SubParameters)
                {
                    Validate(p, errors);
                }
            }
        }
    }
}