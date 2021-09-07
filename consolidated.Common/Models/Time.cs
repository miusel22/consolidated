using System;

namespace consolidated.Common.Models
{
    public class Time
    {
        public int employeId { get; set; }
        public DateTime date { get; set; }
        public bool isConsolidated { get; set; }
        public int type { get; set; }
    }
}
