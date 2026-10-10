using System;
using System.Collections.Generic;
using System.Text;
using MyApp.Domain.Enums;

namespace MyApp.Domain.Entities
{
    public class Exercise
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public EquipmentType EquipmentType { get; set; }

        public string? Description { get; set; }

        public string? Technique { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTimeOffset? DeletedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public ICollection<ExerciseMuscleGroup> ExerciseMuscleGroups { get; set; } = new List<ExerciseMuscleGroup>();

        public ICollection<WorkoutTemplateExercise> WorkoutTemplateExercises { get; set; } = new List<WorkoutTemplateExercise>();

        public ICollection<WorkoutExercise> WorkoutExercises { get; set; } = new List<WorkoutExercise>();

        public ICollection<WorkoutRecord> WorkoutRecords { get; set; } = new List<WorkoutRecord>();
    }
}
