using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.DTO
{
    public record OperationResult
    {
            public bool Success { get; init; }

            public string? ErrorMessage { get; init; }
        
    }
}
