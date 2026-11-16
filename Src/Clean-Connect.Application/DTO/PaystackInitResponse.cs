using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.DTO
{
    public class PaystackInitResponse
    {
        public bool Status { get; set; }
        public string? AuthorizationUrl { get; set; }
        public string? Reference { get; set; }
    }
}
