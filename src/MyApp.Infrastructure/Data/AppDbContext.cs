using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Infrastructure.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Division> Divisions { get; set; }

    public virtual DbSet<Exercise> Exercises { get; set; }

    public virtual DbSet<ExerciseMuscleGroup> ExerciseMuscleGroups { get; set; }

    public virtual DbSet<Gym> Gyms { get; set; }

    public virtual DbSet<Medal> Medals { get; set; }

    public virtual DbSet<MuscleGroup> MuscleGroups { get; set; }

    public virtual DbSet<MuscleLoad> MuscleLoads { get; set; }

    public virtual DbSet<Region> Regions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserMedal> UserMedals { get; set; }

    public virtual DbSet<UserProgress> UserProgresses { get; set; }

    public virtual DbSet<Workout> Workouts { get; set; }

    public virtual DbSet<WorkoutExercise> WorkoutExercises { get; set; }

    public virtual DbSet<WorkoutRecord> WorkoutRecords { get; set; }

    public virtual DbSet<WorkoutSession> WorkoutSessions { get; set; }

    public virtual DbSet<WorkoutSet> WorkoutSets { get; set; }

    public virtual DbSet<WorkoutTemplate> WorkoutTemplates { get; set; }

    public virtual DbSet<WorkoutTemplateExercise> WorkoutTemplateExercises { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("pulseforge", "equipment_type", new[] { "gym", "calisthenics" })
            .HasPostgresEnum("pulseforge", "medal_condition_type", new[] { "workouts_count", "level", "total_xp", "streak_days" })
            .HasPostgresEnum("pulseforge", "session_status", new[] { "planned", "completed", "cancelled" })
            .HasPostgresEnum("pulseforge", "user_role", new[] { "user", "trainer", "admin" });

        modelBuilder.Entity<Division>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("divisions_pkey");

            entity.ToTable("divisions", "pulseforge");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Exercise>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("exercises_pkey");

            entity.ToTable("exercises", "pulseforge");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.Technique).HasColumnName("technique");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<ExerciseMuscleGroup>(entity =>
        {
            entity.HasKey(e => new { e.ExerciseId, e.MuscleGroupId }).HasName("exercise_muscle_groups_pkey");

            entity.ToTable("exercise_muscle_groups", "pulseforge");

            entity.HasIndex(e => e.MuscleGroupId, "emg_muscle_group_idx");

            entity.Property(e => e.ExerciseId).HasColumnName("exercise_id");
            entity.Property(e => e.MuscleGroupId).HasColumnName("muscle_group_id");
            entity.Property(e => e.LoadFactor)
                .HasPrecision(4, 3)
                .HasColumnName("load_factor");

            entity.HasOne(d => d.Exercise).WithMany(p => p.ExerciseMuscleGroups)
                .HasForeignKey(d => d.ExerciseId)
                .HasConstraintName("exercise_muscle_groups_exercise_id_fkey");

            entity.HasOne(d => d.MuscleGroup).WithMany(p => p.ExerciseMuscleGroups)
                .HasForeignKey(d => d.MuscleGroupId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("exercise_muscle_groups_muscle_group_id_fkey");
        });

        modelBuilder.Entity<Gym>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("gyms_pkey");

            entity.ToTable("gyms", "pulseforge");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.RegionId).HasColumnName("region_id");

            entity.HasOne(d => d.Region).WithMany(p => p.Gyms)
                .HasForeignKey(d => d.RegionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("gyms_region_id_fkey");
        });

        modelBuilder.Entity<Medal>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("medals_pkey");

            entity.ToTable("medals", "pulseforge");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.ConditionValue).HasColumnName("condition_value");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasDefaultValueSql("''::text")
                .HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<MuscleGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("muscle_groups_pkey");

            entity.ToTable("muscle_groups", "pulseforge");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(60)
                .HasColumnName("name");
        });

        modelBuilder.Entity<MuscleLoad>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("muscle_load_pkey");

            entity.ToTable("muscle_load", "pulseforge");

            entity.HasIndex(e => e.MuscleGroupId, "muscle_load_group_idx");

            entity.HasIndex(e => new { e.UserId, e.MuscleGroupId }, "muscle_load_user_group_uq").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.LoadValue)
                .HasPrecision(10, 2)
                .HasColumnName("load_value");
            entity.Property(e => e.MuscleGroupId).HasColumnName("muscle_group_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.MuscleGroup).WithMany(p => p.MuscleLoads)
                .HasForeignKey(d => d.MuscleGroupId)
                .HasConstraintName("muscle_load_muscle_group_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.MuscleLoads)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("muscle_load_user_id_fkey");
        });

        modelBuilder.Entity<Region>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("regions_pkey");

            entity.ToTable("regions", "pulseforge");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users", "pulseforge");

            entity.HasIndex(e => e.Id, "users_blocked_idx").HasFilter("is_blocked");

            entity.HasIndex(e => e.DivisionId, "users_division_idx").HasFilter("(division_id IS NOT NULL)");

            entity.HasIndex(e => e.Email, "users_email_uq").IsUnique();

            entity.HasIndex(e => e.GymId, "users_gym_idx").HasFilter("(gym_id IS NOT NULL)");

            entity.HasIndex(e => e.Username, "users_username_uq").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.BirthDate).HasColumnName("birth_date");
            entity.Property(e => e.BlockedAt).HasColumnName("blocked_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DivisionId).HasColumnName("division_id");
            entity.Property(e => e.Email)
                .HasMaxLength(254)
                .HasColumnName("email");
            entity.Property(e => e.GymId).HasColumnName("gym_id");
            entity.Property(e => e.HeightCm)
                .HasPrecision(5, 1)
                .HasColumnName("height_cm");
            entity.Property(e => e.IsBlocked).HasColumnName("is_blocked");
            entity.Property(e => e.IsExcludedFromLeaderboard).HasColumnName("is_excluded_from_leaderboard");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.Username)
                .HasMaxLength(30)
                .HasColumnName("username");
            entity.Property(e => e.WeightKg)
                .HasPrecision(5, 2)
                .HasColumnName("weight_kg");

            entity.HasOne(d => d.Division).WithMany(p => p.Users)
                .HasForeignKey(d => d.DivisionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("users_division_id_fkey");

            entity.HasOne(d => d.Gym).WithMany(p => p.Users)
                .HasForeignKey(d => d.GymId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("users_gym_id_fkey");
        });

        modelBuilder.Entity<UserMedal>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_medals_pkey");

            entity.ToTable("user_medals", "pulseforge");

            entity.HasIndex(e => e.MedalId, "user_medals_medal_idx");

            entity.HasIndex(e => new { e.UserId, e.EarnedAt }, "user_medals_user_earned_idx").IsDescending(false, true);

            entity.HasIndex(e => new { e.UserId, e.MedalId }, "user_medals_user_medal_uq").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.EarnedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("earned_at");
            entity.Property(e => e.MedalId).HasColumnName("medal_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Medal).WithMany(p => p.UserMedals)
                .HasForeignKey(d => d.MedalId)
                .HasConstraintName("user_medals_medal_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.UserMedals)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_medals_user_id_fkey");
        });

        modelBuilder.Entity<UserProgress>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("user_progress_pkey");

            entity.ToTable("user_progress", "pulseforge");

            entity.HasIndex(e => e.Xp, "user_progress_xp_idx").IsDescending();

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.Level)
                .HasDefaultValue(1)
                .HasColumnName("level");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.Xp).HasColumnName("xp");

            entity.HasOne(d => d.User).WithOne(p => p.UserProgress)
                .HasForeignKey<UserProgress>(d => d.UserId)
                .HasConstraintName("user_progress_user_id_fkey");
        });

        modelBuilder.Entity<Workout>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("workouts_pkey");

            entity.ToTable("workouts", "pulseforge");

            entity.HasIndex(e => e.BasedOnTemplateId, "workouts_template_idx").HasFilter("(based_on_template_id IS NOT NULL)");

            entity.HasIndex(e => e.UserId, "workouts_user_idx");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.BasedOnTemplateId).HasColumnName("based_on_template_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.BasedOnTemplate).WithMany(p => p.Workouts)
                .HasForeignKey(d => d.BasedOnTemplateId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("workouts_based_on_template_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Workouts)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("workouts_user_id_fkey");
        });

        modelBuilder.Entity<WorkoutExercise>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("workout_exercises_pkey");

            entity.ToTable("workout_exercises", "pulseforge");

            entity.HasIndex(e => e.ExerciseId, "we_exercise_idx");

            entity.HasIndex(e => new { e.WorkoutId, e.ExerciseId }, "we_workout_exercise_uq").IsUnique();

            entity.HasIndex(e => new { e.WorkoutId, e.Position }, "we_workout_position_uq").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.ExerciseId).HasColumnName("exercise_id");
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.TargetReps).HasColumnName("target_reps");
            entity.Property(e => e.TargetSets).HasColumnName("target_sets");
            entity.Property(e => e.WorkoutId).HasColumnName("workout_id");

            entity.HasOne(d => d.Exercise).WithMany(p => p.WorkoutExercises)
                .HasForeignKey(d => d.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("workout_exercises_exercise_id_fkey");

            entity.HasOne(d => d.Workout).WithMany(p => p.WorkoutExercises)
                .HasForeignKey(d => d.WorkoutId)
                .HasConstraintName("workout_exercises_workout_id_fkey");
        });

        modelBuilder.Entity<WorkoutRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("workout_records_pkey");

            entity.ToTable("workout_records", "pulseforge");

            entity.HasIndex(e => e.ExerciseId, "wr_exercise_idx");

            entity.HasIndex(e => e.SessionId, "wr_session_idx");

            entity.HasIndex(e => e.CreatedAt, "wr_suspicious_idx")
                .IsDescending()
                .HasFilter("is_suspicious");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ExerciseId).HasColumnName("exercise_id");
            entity.Property(e => e.IsSuspicious).HasColumnName("is_suspicious");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.XpEarned).HasColumnName("xp_earned");

            entity.HasOne(d => d.Exercise).WithMany(p => p.WorkoutRecords)
                .HasForeignKey(d => d.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("workout_records_exercise_id_fkey");

            entity.HasOne(d => d.Session).WithMany(p => p.WorkoutRecords)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("workout_records_session_id_fkey");
        });

        modelBuilder.Entity<WorkoutSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("workout_sessions_pkey");

            entity.ToTable("workout_sessions", "pulseforge");

            entity.HasIndex(e => e.ScheduledDate, "ws_date_idx");

            entity.HasIndex(e => e.ScheduledDate, "ws_planned_idx").HasFilter("(status = 'planned'::pulseforge.session_status)");

            entity.HasIndex(e => new { e.WorkoutId, e.ScheduledDate }, "ws_workout_date_idx").IsDescending(false, true);

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ScheduledDate).HasColumnName("scheduled_date");
            entity.Property(e => e.WorkoutId).HasColumnName("workout_id");

            entity.HasOne(d => d.Workout).WithMany(p => p.WorkoutSessions)
                .HasForeignKey(d => d.WorkoutId)
                .HasConstraintName("workout_sessions_workout_id_fkey");
        });

        modelBuilder.Entity<WorkoutSet>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("workout_sets_pkey");

            entity.ToTable("workout_sets", "pulseforge");

            entity.HasIndex(e => new { e.RecordId, e.SetNumber }, "wset_record_number_uq").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.RecordId).HasColumnName("record_id");
            entity.Property(e => e.Reps).HasColumnName("reps");
            entity.Property(e => e.SetNumber).HasColumnName("set_number");
            entity.Property(e => e.Weight)
                .HasPrecision(6, 2)
                .HasColumnName("weight");

            entity.HasOne(d => d.Record).WithMany(p => p.WorkoutSets)
                .HasForeignKey(d => d.RecordId)
                .HasConstraintName("workout_sets_record_id_fkey");
        });

        modelBuilder.Entity<WorkoutTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("workout_templates_pkey");

            entity.ToTable("workout_templates", "pulseforge");

            entity.HasIndex(e => e.TrainerId, "workout_templates_trainer_idx");

            entity.HasIndex(e => new { e.TrainerId, e.Name }, "workout_templates_trainer_name_uq").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.TrainerId).HasColumnName("trainer_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<WorkoutTemplateExercise>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("workout_template_exercises_pkey");

            entity.ToTable("workout_template_exercises", "pulseforge");

            entity.HasIndex(e => e.ExerciseId, "wte_exercise_idx");

            entity.HasIndex(e => new { e.TemplateId, e.ExerciseId }, "wte_template_exercise_uq").IsUnique();

            entity.HasIndex(e => new { e.TemplateId, e.Position }, "wte_template_position_uq").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.ExerciseId).HasColumnName("exercise_id");
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.TargetReps).HasColumnName("target_reps");
            entity.Property(e => e.TargetSets).HasColumnName("target_sets");
            entity.Property(e => e.TemplateId).HasColumnName("template_id");

            entity.HasOne(d => d.Exercise).WithMany(p => p.WorkoutTemplateExercises)
                .HasForeignKey(d => d.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("workout_template_exercises_exercise_id_fkey");

            entity.HasOne(d => d.Template).WithMany(p => p.WorkoutTemplateExercises)
                .HasForeignKey(d => d.TemplateId)
                .HasConstraintName("workout_template_exercises_template_id_fkey");
        });
    }
}
