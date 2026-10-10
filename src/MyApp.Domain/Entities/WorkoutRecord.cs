using System;
using System.Collections.Generic;
using System.Text;

namespace MyApp.Domain.Entities
{
    public class WorkoutRecord
    {
        public long Id { get; set; }

        public long SessionId { get; set; }

        public int ExerciseId { get; set; }

        public int XpEarned { get; set; } = 0;

        public bool IsSuspicious { get; set; } = false;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public WorkoutSession Session { get; set; } = null!;

        public Exercise Exercise { get; set; } = null!;

        public ICollection<WorkoutSet> WorkoutSets { get; set; } = new List<WorkoutSet>();
    }
}
