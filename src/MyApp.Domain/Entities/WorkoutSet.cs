using System;
using System.Collections.Generic;
using System.Text;

namespace MyApp.Domain.Entities
{
    public class WorkoutSet
    {
        public long Id { get; set; }

        public long RecordId { get; set; }

        public short SetNumber { get; set; }

        public short Reps { get; set; }

        public decimal Weight { get; set; } = 0;

        public WorkoutRecord Record { get; set; } = null!;
    }
}
