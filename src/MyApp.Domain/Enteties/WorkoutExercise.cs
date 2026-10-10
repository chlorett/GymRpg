using System;
using System.Collections.Generic;
using System.Text;

namespace MyApp.Domain.Enteties
{
    public class WorkoutExercise

    {

        public long Id { get; set; }

        public long WorkoutId { get; set; }

        public int ExerciseId { get; set; }

        public short Position { get; set; }

        public short TargetSets { get; set; }

        public short TargetReps { get; set; }

        public Workout Workout { get; set; } = null!;

        public Exercise Exercise { get; set; } = null!;

    }

}
