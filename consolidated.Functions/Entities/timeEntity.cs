
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace consolidated.Functions.Entities
{
    public class TimeEntity : TableEntity
    {
        public int employeId { get; set; }
        public DateTime date { get; set; }
        public bool isConsolidated { get; set; }
        public int type { get; set; }

    }
}
