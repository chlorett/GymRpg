using System;
using System.Collections.Generic;
using System.Text;

namespace MyApp.Domain.Entities
{
    public class Gym
    {
        public int Id { get; set; }

        public int RegionId { get; set; }

        public string Name { get; set; } = string.Empty;

        public Region Region { get; set; } = null!;

        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
