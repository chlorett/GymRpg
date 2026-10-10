using System;
using System.Collections.Generic;
using System.Text;

namespace MyApp.Domain.Entities
{
    public class WorkoutTemplateExercise
    {
        public long Id { get; set; }

        public long TemplateId { get; set; }

        public int ExerciseId { get; set; }

        public short Position { get; set; }

        public short TargetSets { get; set; }

        public short TargetReps { get; set; }

        public WorkoutTemplate Template { get; set; } = null!;

        public Exercise Exercise { get; set; } = null!;
    }
}
