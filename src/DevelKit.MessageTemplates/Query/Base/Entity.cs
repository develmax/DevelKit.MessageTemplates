using System;
using System.Collections.Generic;
// ReSharper disable MemberCanBePrivate.Global

// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace DevelKit.MessageTemplates.Query.Base;

public class Entity
{
    public Entity(string entityName)
    {
        LogicalName = entityName;
    }

    public Guid Id { get; set; }

    public Dictionary<string, object?> Attributes { get; set; } = new();
    // public FormattedValueCollection FormattedValues { get; set; }
    // public EntityState? EntityState { get; set; }
    public string LogicalName { get; set; }
    // public RelatedEntityCollection RelatedEntities { get; set; }

    public object? this[string name]
    {
        get => Attributes[name];
        set => Attributes[name] = value;
    }

    public bool Contains(string name)
    {
        return Attributes.ContainsKey(name);
    }

    public T? GetAttributeValue<T>(string name)
    {
        if (Attributes.TryGetValue(name, out var value))
        {
            if (value == null)
            {
                return default(T);
            }

            return (T)value;
        }

        return default(T);
    }

    public T? GetAliasedValue<T>(string name)
    {
        if (Attributes.TryGetValue(name, out var value) && value is AliasedValue aliasedValue)
        {
            if (aliasedValue.Value == null)
            {
                return default(T);
            }

            return (T)aliasedValue.Value;
        }

        return default(T);
    }
}