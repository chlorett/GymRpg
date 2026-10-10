using System;
using System.Collections.Generic;
using System.Text;
using MyApp.Domain.Enums;

namespace MyApp.Domain.Enteties
{
    public class User

    {

        public long Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public UserRole Role { get; set; } = UserRole.User;

        public decimal? WeightKg { get; set; }

        public decimal? HeightCm { get; set; }

        public DateTime? BirthDate { get; set; }

        public int? GymId { get; set; }

        public int? DivisionId { get; set; }

        public bool IsBlocked { get; set; } = false;

        public DateTimeOffset? BlockedAt { get; set; }

        public bool IsExcludedFromLeaderboard { get; set; } = false;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public Gym? Gym { get; set; }

        public Division? Division { get; set; }

        public UserProgress? UserProgress { get; set; }

        public ICollection<UserMedal> UserMedals { get; set; } = new List<UserMedal>();

        public ICollection<MuscleLoad> MuscleLoads { get; set; } = new List<MuscleLoad>();

        public ICollection<WorkoutTemplate> AuthoredTemplates { get; set; } = new List<WorkoutTemplate>();

        public ICollection<Workout> Workouts { get; set; } = new List<Workout>();

    }

}
