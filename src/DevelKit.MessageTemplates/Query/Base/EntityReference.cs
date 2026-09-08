using System;

namespace DevelKit.MessageTemplates.Query.Base;

public class EntityReference
{
    public EntityReference(string logicalName, Guid id)
    {
        LogicalName = logicalName;
        Id = id;
    }

    public string LogicalName { get; set; }
    public Guid Id { get; set; }
}