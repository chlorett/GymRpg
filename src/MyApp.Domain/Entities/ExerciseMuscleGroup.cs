using System;
using System.Collections.Generic;
using System.Text;

namespace MyApp.Domain.Entities
{
    public class ExerciseMuscleGroup
    {
        public int ExerciseId { get; set; }

        public short MuscleGroupId { get; set; }

        public decimal LoadFactor { get; set; }

        public Exercise Exercise { get; set; } = null!;

        public MuscleGroup MuscleGroup { get; set; } = null!;
    }
}
