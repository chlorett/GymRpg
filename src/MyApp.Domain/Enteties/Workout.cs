using System;
using System.Collections.Generic;
using System.Text;

namespace MyApp.Domain.Enteties
{
    public class Workout

    {

        public long Id { get; set; }

        public long UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        public long? BasedOnTemplateId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public User User { get; set; } = null!;

        public WorkoutTemplate? BasedOnTemplate { get; set; }

        public ICollection<WorkoutExercise> WorkoutExercises { get; set; } = new List<WorkoutExercise>();

        public ICollection<WorkoutSession> WorkoutSessions { get; set; } = new List<WorkoutSession>();

    }

}
