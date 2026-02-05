using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MigrationTool.Shared
{
    public class ConnectionsService
    {
        // We store the builder itself so it's easy to access properties
        public static SqlConnectionStringBuilder SourceBuilder { get; set; } = new();
        public static SqlConnectionStringBuilder DestBuilder { get; set; } = new();
    }
}
