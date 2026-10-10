using System;
using System.Collections.Generic;
using System.Text;

namespace MyApp.Domain.Enteties
{
    public class Division

    {

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public ICollection<User> Users { get; set; } = new List<User>();

    }

}
