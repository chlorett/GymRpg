using System;
using System.Collections.Generic;
using System.Text;
using MyApp.Domain.Enums;

namespace MyApp.Domain.Enteties
{
    public class Medal

    {

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public MedalConditionType ConditionType { get; set; }

        public int ConditionValue { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public ICollection<UserMedal> UserMedals { get; set; } = new List<UserMedal>();

    }

}
