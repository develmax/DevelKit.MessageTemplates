using System;
using System.Collections.Generic;
using DevelKit.MessageTemplates.Parameters;

// ReSharper disable UnusedMember.Local
#pragma warning disable IDE0051 // Remove unused private members
namespace DevelKit.MessageTemplates
{
    /// <summary>
    /// Разбирает параметры, связи, форматы и условия, сохраняя координаты в исходном тексте.
    /// Нераспознанные фрагменты остаются текстом: это не строгая проверка синтаксиса.
    /// </summary>
    public class TextTemplateParser
    {
        private static readonly Dictionary<string, TextTemplateParameterConditionOperator> Operators =
            new()
            {
                { "!=", TextTemplateParameterConditionOperator.NotEqual },
                { "<=", TextTemplateParameterConditionOperator.LessEqual },
                { ">=", TextTemplateParameterConditionOperator.GreaterEqual },
                { "=", TextTemplateParameterConditionOperator.Equal },
                { ">", TextTemplateParameterConditionOperator.GreaterThan },
                { "<", TextTemplateParameterConditionOperator.LessThan }
            };

        private readonly string? _text;

        private int _i;
        private int _depth;

        private TextTemplate _template = null!;

        private TextTemplateParser(string? str)
        {
            _text = str;
        }

        /// <summary>Создаёт отдельный экземпляр парсера для каждого вызова. Для null возвращает пустое дерево.</summary>
        /// <exception cref="FormatException">Вложенность поля превысила 64 уровня.</exception>
        public static TextTemplate Parse(string? text)
        {
            var parser = new TextTemplateParser(text);
            return parser.Parse();
        }

        private TextTemplate Parse()
        {
            _i = 0;
            _template = new TextTemplate(_text);

            if (string.IsNullOrEmpty(_text))
            {
                return _template;
            }

            while (!End())
            {
                _ = ParseSpaces();

                if (!ParseParameter(out var parameter))
                {
                    if (!Next())
                    {
                        break;
                    }
                }
                else
                {
                    _template.Parameters.Add(parameter!);
                }
            }

            return _template;
        }

        private void Revert(int k)
        {
            _i = k;
        }

        private bool End()
        {
            return _i == _text!.Length;
        }

        private bool Next(int n = 1)
        {
            if (_i + n > _text!.Length)
            {
                return false;
            }

            _i += n;

            return true;
        }

        private char Get()
        {
            return _text![_i];
        }

        private string Get(int n)
        {
            return _text!.Substring(_i, n);
        }

        private bool Read(char c)
        {
            if (End())
            {
                return false;
            }

            var v = Get();
            if (v == c)
            {
                _ = Next();
            }
            else
            {
                return false;
            }

            return true;
        }

        private bool Read(string s)
        {
            if (_text!.Length - _i < s.Length)
            {
                return false;
            }

            var v = Get(s.Length);
            if (v == s)
            {
                _ = Next(s.Length);
            }
            else
            {
                return false;
            }

            return true;
        }

        private bool Read(IEnumerable<string> strings)
        {
            foreach (var s in strings)
            {
                if (_text!.Length - _i < s.Length)
                {
                    continue;
                }

                var v = Get(s.Length);
                if (v == s)
                {
                    _ = Next(s.Length);

                    return true;
                }
            }

            return false;
        }

        private bool Read(IEnumerable<char> chars)
        {
            if (_text!.Length - _i < 1)
            {
                return false;
            }

            foreach (var c in chars)
            {
                var v = Get();
                if (v == c)
                {
                    _ = Next();

                    return true;
                }
            }

            return false;
        }

        private bool ParseSpace()
        {
            if (!Read(new[] { ' ', '\t', '\r', '\n' }))
            {
                return false;
            }

            return true;
        }

        private bool ParseSpaces()
        {
            var parsed = false;

            while (ParseSpace())
            {
                parsed = true;
            }

            return parsed;
        }

        private bool ParseExtendedFormat(out TextTemplateParameter? parameter)
        {
            return ParseParameter(out parameter);
        }

        private bool ParseParameter(out TextTemplateParameter? parameter)
        {
            TextTemplateParameter? tmp = null;

            var parsed = ParseWithRevert(() =>
            {
                var startIndex = _i;

                if (!Read("{{"))
                {
                    return false;
                }

                _ = ParseSpaces();

                if (!ParseName(out var name))
                {
                    return false;
                }

                _ = ParseSpaces();
                if (!Read(':'))
                {
                    return false;
                }

                _ = ParseSpaces();

                if (!ParseField(out var field))
                {
                    return false;
                }

                _ = ParseSpaces();
                if (!Read("}}"))
                {
                    return false;
                }

                tmp = CreateParameterByField(startIndex, _i - startIndex, name!, null, field!);

                return true;
            });

            parameter = tmp;
            return parsed;
        }

        private TextTemplateParameter CreateParameterByField(int startIndex, int length, string? entityName, TextTemplateParameter? parent, TextTemplateParameterField field)
        {
            var parameter = new TextTemplateParameter(_template, startIndex, length)
            {
                EntityName = entityName,
                Parent = parent
            };

            if (field.OperatorIf != null)
            {
                parameter.Type = TextTemplateParameterType.Operator;
                parameter.Operator = TextTemplateParameterOperator.If;
                parameter.ConditionOperator = field.OperatorIf.Condition.ConditionOperator;
                parameter.ConditionValue = field.OperatorIf.Condition.Value;
                // Порядок фиксирован: условие, истинная ветвь, ложная ветвь.
                // Форматтер использует эти позиции при выборе значения.
                parameter.SubParameters = new List<TextTemplateParameter>
                {
                    CreateParameterByField(field.StartIndex, field.Length, null, parameter, field.OperatorIf.Condition.Field),
                    CreateParameterByField(field.StartIndex, field.Length, null, parameter, field.OperatorIf.Field1),
                    CreateParameterByField(field.StartIndex, field.Length, null, parameter, field.OperatorIf.Field2)
                };
            }
            else
            {
                parameter.FieldName = field.Name;
                parameter.Format = field.Format;

                if (field.ExtendedFormat != null)
                {
                    // Связь содержит один вложенный параметр следующей сущности.
                    parameter.SubParameters = new List<TextTemplateParameter>
                    {
                        field.ExtendedFormat
                    };

                    parameter.SubParameters[0].Parent = parameter;
                }
            }

            return parameter;
        }

        private bool ParseName(out string? name)
        {
            return ParseChars(out name, c => char.IsLetter(c) || c == '_' || char.IsNumber(c), true);
        }

        private bool ParseChars(out string? chars, Func<char, bool> conditionChar, bool startRequired = false)
        {
            chars = null;

            var parsed = false;
            var startIndex = _i;

            if (startRequired)
            {
                if (End() || !conditionChar(Get()))
                {
                    return false;
                }

                _ = Next();

                parsed = true;
            }

            while (!End() && conditionChar(Get()))
            {
                _ = Next();

                parsed = true;
            }

            if (parsed)
            {
                chars = _text!.Substring(startIndex, _i - startIndex);
            }

            return parsed;
        }

        private bool ParseField(out TextTemplateParameterField? field)
        {
            // Ограничиваем глубину взаимных вызовов поля, связи и условия.
            if (++_depth > 64) throw new FormatException("Template nesting exceeds 64 levels.");
            try { return ParseFieldCore(out field); }
            finally { _depth--; }
        }

        private bool ParseFieldCore(out TextTemplateParameterField? field)
        {
            TextTemplateParameterField? tmp = null;
            var startIndex = _i;

            if (ParseIf(out var operatorIf))
            {
                tmp = new TextTemplateParameterField
                {
                    StartIndex = startIndex,
                    Length = _i - startIndex,
                    OperatorIf = operatorIf
                };

                field = tmp;

                return true;
            }

            var parsed = ParseWithRevert(() =>
            {
                if (!ParseName(out var name))
                {
                    return false;
                }

                _ = ParseSpaces();

                if (ParseExtendedFormat(out var parameter))
                {
                    tmp = new TextTemplateParameterField()
                    {
                        StartIndex = startIndex,
                        Length = _i - startIndex,
                        Name = name!,
                        ExtendedFormat = parameter!
                    };

                    return true;
                }

                if (ParseFormat(out var format))
                {
                    tmp = new TextTemplateParameterField()
                    {
                        StartIndex = startIndex,
                        Length = _i - startIndex,
                        Name = name!,
                        Format = format
                    };
                    return true;
                }

                tmp = new TextTemplateParameterField()
                {
                    StartIndex = startIndex,
                    Length = _i - startIndex,
                    Name = name!
                };

                return true;
            });

            field = tmp;

            return parsed;
        }

        private bool ParseIf(out TextTemplateParameterOperatorIf? operatorIf)
        {
            TextTemplateParameterOperatorIf? tmp = null;

            var parsed = ParseWithRevert(() =>
            {
                if (!Read("if"))
                {
                    return false;
                }

                _ = ParseSpaces();
                if (!Read("[["))
                {
                    return false;
                }

                _ = ParseSpaces();
                if (!ParseIfConditionLong(out var condition))
                {
                    return false;
                }

                _ = ParseSpaces();
                if (!Read('?'))
                {
                    return false;
                }

                _ = ParseSpaces();

                if (!ParseField(out var field1))
                {
                    return false;
                }

                _ = ParseSpaces();
                if (!Read(';'))
                {
                    return false;
                }

                _ = ParseSpaces();

                if (!ParseField(out var field2))
                {
                    return false;
                }

                _ = ParseSpaces();
                if (!Read("]]"))
                {
                    return false;
                }

                tmp = new TextTemplateParameterOperatorIf()
                      {
                          Condition = condition!,
                          Field1 = field1!,
                          Field2 = field2!
                      };

                return true;
            });

            operatorIf = tmp;

            return parsed;
        }

        private TextTemplateParameterConditionOperator GetConditionOperator(string conditionOperatorKey)
        {
            return Operators[conditionOperatorKey];
        }

        private bool ParseIfConditionOperator(out TextTemplateParameterConditionOperator conditionOperator)
        {
            conditionOperator = TextTemplateParameterConditionOperator.Equal;

            var startIndex = _i;
            // Длинные операторы проверяются первыми: иначе <= разобрался бы как <.
            var parsed = Read(new[] { "!=", "<=", ">=", "=", ">", "<" });

            if (parsed)
            {
                conditionOperator = GetConditionOperator(_text!.Substring(startIndex, _i - startIndex));
            }

            return parsed;
        }

        private bool ParseString(out string? value)
        {
            string? tmp = null;

            var parsed = ParseWithRevert(() =>
            {
                if (!Read('\''))
                {
                    return false;
                }

                while (ParseChars(out _, c => c != '\'') || Read("''"))
                {
                    // ignored
                }

                if (!Read('\''))
                {
                    return false;
                }

                return true;
            }, (start, len) => tmp = _text!.Substring(start + 1, len - 2));

            value = tmp;

            return parsed;
        }

        private bool ParseBool(out bool value)
        {
            value = false;

            if (Read("true"))
            {
                value = true;
                return true;
            }

            if (Read("false"))
            {
                return true;
            }

            return false;
        }

        private bool ParseNull()
        {
            if (Read("null"))
            {
                return true;
            }

            return false;
        }

        private bool ParseInt(out int value)
        {
            value = 0;

            var parsed = ParseChars(out var chars, char.IsDigit, true);
            if (parsed)
            {
                value = int.Parse(chars!);
            }

            return parsed;
        }


        private bool ParseWithRevert(Func<bool> parseFunc, Action<int, int>? successAction = null, Action? failedAction = null)
        {
            // Неудачная ветвь грамматики не должна сдвигать начало следующей попытки.
            var save = _i;
            if (!parseFunc())
            {
                Revert(save);

                if (failedAction != null)
                {
                    failedAction();
                }

                return false;
            }

            if (successAction != null)
            {
                successAction(save, _i - save);
            }

            return true;
        }

        private bool ParseStatementValue(out object? value)
        {
            value = null;

            if (ParseString(out var strValue))
            {
                value = strValue;
                return true;
            }

            if (ParseInt(out var intValue))
            {
                value = intValue;
                return true;
            }

            if (ParseBool(out var boolValue))
            {
                value = boolValue;
                return true;
            }

            if (ParseNull())
            {
                return true;
            }

            return false;
        }



        private bool ParseIfConditionLong(out TextTemplateParameterCondition? condition)
        {
            TextTemplateParameterCondition? tmp = null;

            var parsed = ParseWithRevert(() =>
            {

                if (!ParseField(out var field))
                {
                    return false;
                }

                _ = ParseSpaces();

                if (!ParseIfConditionOperator(out var conditionOperator))
                {
                    return false;
                }

                _ = ParseSpaces();

                if (!ParseStatementValue(out var value))
                {
                    return false;
                }

                _ = ParseSpaces();

                tmp = new TextTemplateParameterCondition()
                {
                    Field = field!,
                    ConditionOperator = conditionOperator,
                    Value = value
                };

                return true;
            });

            condition = tmp;

            return parsed;
        }

        private bool InRange(char c, char startChar, char endChar)
        {
            var startCode = (int)startChar;
            var endCode = (int)endChar;

            var value = c;
            var valueCode = (int)value;

            return valueCode >= startCode && valueCode <= endCode;
        }

        private bool ParseFormatValue(out string? formatValue)
        {
            formatValue = null;

            var startIndex = _i;
            var parsed = ParseChars(out _, c => c != '(' && c != ')', true);

            if (parsed)
            {
                formatValue = _text!.Substring(startIndex, _i - startIndex);
            }

            return parsed;
        }

        private bool ParseFormat(out string? format)
        {
            string? formatTmp = null;

            var parsed = ParseWithRevert(() =>
            {
                if (!Read('('))
                {
                    return false;
                }

                if (!ParseFormatValue(out _))
                {
                    return false;
                }

                if (!Read(')'))
                {
                    return false;
                }

                return true;
            }, (start, len) => formatTmp = _text!.Substring(start + 1, len - 2));

            format = formatTmp;

            return parsed;
        }
    }
}