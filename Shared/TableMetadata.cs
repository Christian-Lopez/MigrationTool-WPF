using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MigrationTool.Shared
{
    public class TableMetadata
    {
        public required string Name { get; set; }
        public required string Schema { get; set; }
    }
}
