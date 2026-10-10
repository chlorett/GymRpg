using System;
using System.Collections.Generic;
using System.Text;

namespace MyApp.Domain.Enteties
{
    public class UserProgress

    {

        public long UserId { get; set; }

        public long Xp { get; set; } = 0;

        public int Level { get; set; } = 1;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public User User { get; set; } = null!;

    }

}
