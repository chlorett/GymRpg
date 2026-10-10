using System;
using System.Collections.Generic;
using System.Text;

namespace MyApp.Domain.Enteties
{
    public class UserMedal

    {

        public long Id { get; set; }

        public long UserId { get; set; }

        public int MedalId { get; set; }

        public DateTimeOffset EarnedAt { get; set; } = DateTimeOffset.UtcNow;

        public User User { get; set; } = null!;

        public Medal Medal { get; set; } = null!;

    }

}
