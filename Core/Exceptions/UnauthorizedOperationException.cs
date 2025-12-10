using System;

namespace Core.Exceptions
{
    /// <summary>
    /// Unauthorized operation exception
    /// </summary>
    public class UnauthorizedOperationException : UnauthorizedAccessException
    {
        public UnauthorizedOperationException(string message) : base(message)
        {
        }

        public UnauthorizedOperationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

