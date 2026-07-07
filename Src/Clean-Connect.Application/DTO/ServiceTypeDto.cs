using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.DTO
{
    public record ServiceTypeDto
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public decimal Amount { get; set; }
       
        public string? ModifiedBy {  get; set; }

       
    }
}
