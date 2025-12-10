using System;

namespace Core.Exceptions
{
    /// <summary>
    /// Entity not found exception
    /// </summary>
    public class EntityNotFoundException : Exception
    {
        public string EntityName { get; }
        public object EntityId { get; }

        public EntityNotFoundException(string entityName, object entityId) 
            : base($"{entityName} with id '{entityId}' not found")
        {
            EntityName = entityName;
            EntityId = entityId;
        }

        public EntityNotFoundException(string entityName, object entityId, Exception innerException) 
            : base($"{entityName} with id '{entityId}' not found", innerException)
        {
            EntityName = entityName;
            EntityId = entityId;
        }
    }
}

