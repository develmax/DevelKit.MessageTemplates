using System;
using System.Linq;
using System.Text;
using DevelKit.MessageTemplates.Parameters;

namespace DevelKit.MessageTemplates
{
    /// <summary>Собирает текст по дереву, получая готовые значения через переданную функцию.</summary>
    public static class TextTemplateFormatter
    {
        private static TextTemplateParameter GetInnerTemplate(TextTemplateParameter templateParameter)
        {
            while (templateParameter.Type == TextTemplateParameterType.Field && templateParameter.HasSubParameters)
            {
                templateParameter = templateParameter.SubParameters!.First();
            }

            return templateParameter;
        }

        /// <summary>Копирует текст между параметрами и подставляет выбранные значения.</summary>
        /// <remarks>
        /// Форматы используют текущую культуру .NET. Часовой пояс и кодирование HTML задаёт приложение.
        /// При null удаляются пробельные символы слева от параметра, включая переводы строк.
        /// </remarks>
        public static string Format(TextTemplate textTemplate, Func<TextTemplateParameter, object?> getReplacement)
        {
            if (textTemplate.Value is null) return string.Empty;
            var sb = new StringBuilder();
            var previousTemplateEndIndex = 0;

            foreach (var template in textTemplate.Parameters)
            {
                if (previousTemplateEndIndex < template.StartIndex)
                {
                    _ = sb.Append(textTemplate.Value!.Substring(previousTemplateEndIndex, template.StartIndex - previousTemplateEndIndex));
                }

                WriteTemplateReplacement(sb, template, getReplacement);

                previousTemplateEndIndex = template.StartIndex + template.Length;
            }

            if (previousTemplateEndIndex < textTemplate.Value!.Length)
            {
                _ = sb.Append(textTemplate.Value.Substring(previousTemplateEndIndex, textTemplate.Value.Length - previousTemplateEndIndex));
            }

            return sb.ToString();
        }

        private static void WriteTemplateInnerReplacement(StringBuilder buffer, TextTemplateParameter template, Func<TextTemplateParameter, object?> getReplacement)
        {
            var value = getReplacement(template);
            if (value != null)
            {
                WriteValue(buffer, template.Format, value);
            }
            else
            {
                // Первый параметр может быть пустым: перед чтением последнего символа проверяем длину.
                // Текст справа ещё не записан, поэтому его пробелы и пунктуация сохраняются.
                while (buffer.Length > 0 && char.IsWhiteSpace(buffer[buffer.Length - 1]))
                {
                    _ = buffer.Remove(buffer.Length - 1, 1);
                }
            }
        }

        private static bool IsNumber(this object value)
        {
            switch (value)
            {
                case sbyte:
                case byte:
                case short:
                case ushort:
                case int:
                case uint:
                case long:
                case ulong:
                case float:
                case double:
                case decimal:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsComparable(this object value)
        {
            return value is IComparable;
        }

        private static bool ExecuteCondition(TextTemplateParameterConditionOperator @operator, object? value1, object? value2)
        {
            var result = false;

            switch (@operator)
            {
                case TextTemplateParameterConditionOperator.Equal:
                case TextTemplateParameterConditionOperator.NotEqual:
                {
                    if (value1 == null && value2 == null)
                    {
                        result = true;
                    }
                    else if (value1 == null || value2 == null)
                    {
                        result = false;
                    }
                    else
                    {
                        // Сохраняем историческую семантику: Int32(3) не равен Int64(3).
                        // Согласование типов выполняется до передачи значений форматтеру.
                        result = value1.Equals(value2);
                    }

                    if (@operator == TextTemplateParameterConditionOperator.NotEqual)
                    {
                        result = !result;
                    }

                    break;
                }
                case TextTemplateParameterConditionOperator.LessEqual:
                case TextTemplateParameterConditionOperator.GreaterEqual:
                case TextTemplateParameterConditionOperator.GreaterThan:
                case TextTemplateParameterConditionOperator.LessThan:
                {
                    if (value1 == null || value2 == null)
                    {
                        throw new NullReferenceException("The operator not supported compare null reference values.");
                    }

                    if (!value1.IsNumber() || !value2.IsNumber())
                    {
                        throw new Exception("The selected operator option is only applicable to numeric values.");
                    }

                    if (!value1.IsComparable() || !value2.IsComparable())
                    {
                        throw new Exception("The selected values not implementation IComparable interface.");
                    }

                    var v1 = (IComparable)value1;
                    var v2 = (IComparable)value2;

                    var compareResult = v1.CompareTo(v2);

                    switch (@operator)
                    {
                        case TextTemplateParameterConditionOperator.LessEqual:
                            result = compareResult <= 0;
                            break;
                        case TextTemplateParameterConditionOperator.GreaterEqual:
                            result = compareResult >= 0;
                            break;
                        case TextTemplateParameterConditionOperator.GreaterThan:
                            result = compareResult > 0;
                            break;
                        case TextTemplateParameterConditionOperator.LessThan:
                            result = compareResult < 0;
                            break;
                    }

                    break;
                }
                default:
                    throw new Exception("The selected operator is not defined.");
            }

            return result;
        }

        private static void WriteTemplateReplacement(StringBuilder buffer, TextTemplateParameter template, Func<TextTemplateParameter, object?> getReplacement)
        {
            // Узлы переходов описывают путь чтения. Выводится конечное поле либо выбранная ветвь.
            var innerTemplate = GetInnerTemplate(template);
            if (innerTemplate.Type == TextTemplateParameterType.Operator)
            {
                if (innerTemplate.Operator == TextTemplateParameterOperator.If)
                {
                    var conditionalField = innerTemplate.SubParameters![0];
                    var conditionalFieldInner = GetInnerTemplate(conditionalField);
                    var conditionalFieldInnerValue = getReplacement(conditionalFieldInner);

                    if (innerTemplate.ConditionOperator.HasValue)
                    {
                        if (ExecuteCondition(innerTemplate.ConditionOperator.Value, conditionalFieldInnerValue, innerTemplate.ConditionValue))
                        {
                            var valueField = innerTemplate.SubParameters[1];
                            var valueFieldInner = GetInnerTemplate(valueField);

                            if (valueFieldInner.Type == TextTemplateParameterType.Operator)
                            {
                                WriteTemplateReplacement(buffer, valueFieldInner, getReplacement);
                            }
                            else
                            {
                                WriteTemplateInnerReplacement(buffer, valueFieldInner, getReplacement);
                            }
                        }
                        else
                        {
                            var valueField2 = innerTemplate.SubParameters[2];
                            var valueField2Inner = GetInnerTemplate(valueField2);

                            if (valueField2Inner.Type == TextTemplateParameterType.Operator)
                            {
                                WriteTemplateReplacement(buffer, valueField2Inner, getReplacement);
                            }
                            else
                            {
                                WriteTemplateInnerReplacement(buffer, valueField2Inner, getReplacement);
                            }
                        }
                    }
                }
            }
            else
            {
                WriteTemplateInnerReplacement(buffer, innerTemplate, getReplacement);
            }
        }

        private static void WriteValue(StringBuilder buffer, string? format, object? value)
        {
            _ = !string.IsNullOrEmpty(format)
                ? buffer.AppendFormat('{' + "0:" + format + '}', value)
                : buffer.Append($"{value}");
        }
    }
}