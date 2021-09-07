using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace consolidated.Functions.Entities
{
    public class consolidatedEntity : TableEntity
    {
        public int employeId { get; set; }
        public DateTime date { get; set; }
        public int MinutesWork { get; set; }
    }
}
