using System;
using System.Collections.Generic;
using System.Text;
using MyApp.Domain.Enums;

namespace MyApp.Domain.Entities
{
    public class WorkoutSession
    {
        public long Id { get; set; }

        public long WorkoutId { get; set; }

        public DateTime ScheduledDate { get; set; }

        public SessionStatus Status { get; set; } = SessionStatus.Planned;

        public DateTimeOffset? CompletedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public Workout Workout { get; set; } = null!;

        public ICollection<WorkoutRecord> WorkoutRecords { get; set; } = new List<WorkoutRecord>();
    }
}
