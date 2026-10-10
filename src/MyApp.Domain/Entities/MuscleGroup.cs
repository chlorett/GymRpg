using System;
using System.Collections.Generic;
using System.Text;

namespace MyApp.Domain.Entities
{
    public class MuscleGroup
    {
        public short Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public ICollection<ExerciseMuscleGroup> ExerciseMuscleGroups { get; set; } = new List<ExerciseMuscleGroup>();

        public ICollection<MuscleLoad> MuscleLoads { get; set; } = new List<MuscleLoad>();
    }
}
