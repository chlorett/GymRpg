using System;
using System.Collections.Generic;
using System.Text;

namespace MyApp.Domain.Enteties
{
    public class Region

    {

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public ICollection<Gym> Gyms { get; set; } = new List<Gym>();

    }

}
