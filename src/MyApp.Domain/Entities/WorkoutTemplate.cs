using System;
using System.Collections.Generic;
using System.Text;
using MyApp.Domain.Enums;

namespace MyApp.Domain.Entities
{
    public class WorkoutTemplate
    {
        public long Id { get; set; }

        public long TrainerId { get; set; }

        public UserRole TrainerRole { get; set; } = UserRole.Trainer;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public User Trainer { get; set; } = null!;

        public ICollection<WorkoutTemplateExercise> WorkoutTemplateExercises { get; set; } = new List<WorkoutTemplateExercise>();

        public ICollection<Workout> Workouts { get; set; } = new List<Workout>();
    }
}
