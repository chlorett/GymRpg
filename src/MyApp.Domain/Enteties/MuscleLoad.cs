using System;
using System.Collections.Generic;
using System.Text;

namespace MyApp.Domain.Enteties
{
    public class MuscleLoad

    {

        public long Id { get; set; }

        public long UserId { get; set; }

        public short MuscleGroupId { get; set; }

        public decimal LoadValue { get; set; } = 0;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public User User { get; set; } = null!;

        public MuscleGroup MuscleGroup { get; set; } = null!;

    }

}
