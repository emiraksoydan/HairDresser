using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentValidation.Results;

namespace Core.CrossCuttingConcerns.Validation
{
    public class ClientValidationException : Exception
    {
        public List<ValidationFailure> Errors { get; }

        public ClientValidationException(List<ValidationFailure> errors)
            : base("Validation failed")
        {
            Errors = errors;
        }
    }
}
