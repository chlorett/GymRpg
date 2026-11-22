using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using Xunit;

namespace MyApp.Tests;

public sealed class DivisionQualificationServiceTests
{
    private const int AthleteId = 1;
    private const int NoviceId = 1;
    private const int AmateurId = 2;
    private const int AdvancedId = 3;
    private const int ProId = 4;
    private const int EliteId = 5;

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUserProgressRepository> _progress = new();
    private readonly FakeLogger<DivisionQualificationService> _logger = new();
    private readonly DivisionQualificationService _service;

    public DivisionQualificationServiceTests()
    {
        _users
            .Setup(u => u.GetDivisionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDivisions());
        _users
            .Setup(u => u.GetGymsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Gym>
            {
                new() { Id = 10, RegionId = 1, Name = "Iron Temple" },
                new() { Id = 11, RegionId = 1, Name = "Alpha Gym" },
            });
        _users
            .Setup(u => u.UpdateAffiliationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _progress
            .Setup(p => p.GetLeaderboardUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        SetUser(divisionId: AmateurId);
        SetProgress(AthleteId, 1500, 2);

        _service = new DivisionQualificationService(_users.Object, _progress.Object, _logger);
    }

    [Fact]
    public async Task GetOverview_ReturnsThresholdsAndLevelsForEveryDivision()
    {
        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        Assert.True(result.IsSuccess);
        var rows = result.Value!.Divisions;
        Assert.Equal(new long[] { 0, 1000, 3000, 6000, 10000 }, rows.Select(r => r.MinXp));
        Assert.Equal(new[] { 1, 2, 4, 7, 11 }, rows.Select(r => r.MinLevel));
        Assert.Equal(new[] { "Новачок", "Любитель", "Просунутий", "Професіонал", "Еліта" }, rows.Select(r => r.Name));
    }

    [Fact]
    public async Task GetOverview_MarksCurrentDivisionAndReachedThresholds()
    {
        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        var rows = result.Value!.Divisions;
        Assert.Equal(AmateurId, rows.Single(r => r.IsCurrent).DivisionId);
        Assert.Equal(new[] { true, true, false, false, false }, rows.Select(r => r.IsReached));
    }

    [Fact]
    public async Task GetOverview_CalculatesXpToNextDivisionWhenNotEnoughXp()
    {
        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        var overview = result.Value!;
        Assert.Equal("Любитель", overview.CurrentDivisionName);
        Assert.Equal(1500, overview.CurrentXp);
        Assert.Equal(2, overview.CurrentLevel);
        Assert.Equal(AdvancedId, overview.NextDivisionId);
        Assert.Equal("Просунутий", overview.NextDivisionName);
        Assert.Equal(1500, overview.XpToNextDivision);
        Assert.False(overview.CanPromote);
    }

    [Theory]
    [InlineData(2999, false, 1)]
    [InlineData(3000, true, 0)]
    [InlineData(4500, true, 0)]
    public async Task GetOverview_CanPromoteOnlyWhenXpReachesNextThreshold(long xp, bool expected, long xpLeft)
    {
        SetProgress(AthleteId, xp, 3);

        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        Assert.Equal(expected, result.Value!.CanPromote);
        Assert.Equal(xpLeft, result.Value.XpToNextDivision);
    }

    [Fact]
    public async Task GetOverview_WhenAthleteIsInTopDivision_HasNoNextDivision()
    {
        SetUser(divisionId: EliteId);
        SetProgress(AthleteId, 25000, 26);

        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        var overview = result.Value!;
        Assert.Null(overview.NextDivisionId);
        Assert.Null(overview.NextDivisionName);
        Assert.Equal(0, overview.XpToNextDivision);
        Assert.False(overview.CanPromote);
    }

    [Fact]
    public async Task GetOverview_WhenUserHasNoDivision_OffersFirstDivisionAndShowsItAsSelected()
    {
        SetUser(divisionId: null);
        SetProgress(AthleteId, 0, 1);

        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        var overview = result.Value!;
        Assert.Null(overview.CurrentDivisionId);
        Assert.Equal("—", overview.CurrentDivisionName);
        Assert.Equal(NoviceId, overview.NextDivisionId);
        Assert.Equal(NoviceId, overview.SelectedDivisionId);
        Assert.True(overview.CanPromote);
    }

    [Fact]
    public async Task GetOverview_WhenProgressMissing_TreatsXpAsZeroAndLevelAsOne()
    {
        _progress
            .Setup(p => p.GetByUserIdAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProgress?)null);

        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        Assert.Equal(0, result.Value!.CurrentXp);
        Assert.Equal(1, result.Value.CurrentLevel);
        Assert.Equal(3000, result.Value.XpToNextDivision);
    }

    [Fact]
    public async Task GetOverview_WhenUserIsBlocked_CannotPromoteEvenWithEnoughXp()
    {
        SetUser(divisionId: AmateurId, isBlocked: true);
        SetProgress(AthleteId, 5000, 6);

        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.CanPromote);
    }

    [Fact]
    public async Task GetOverview_WhenUserExcludedFromLeaderboard_CannotPromote()
    {
        SetUser(divisionId: AmateurId, isExcluded: true);
        SetProgress(AthleteId, 5000, 6);

        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        Assert.False(result.Value!.CanPromote);
    }

    [Fact]
    public async Task GetOverview_DefaultsStandingsToCurrentDivision()
    {
        SetParticipants(
            Participant(1, "hero", AmateurId, 1500, 10),
            Participant(2, "rival", AmateurId, 2500, 11),
            Participant(3, "newbie", NoviceId, 300, null));

        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        var overview = result.Value!;
        Assert.Equal(AmateurId, overview.SelectedDivisionId);
        Assert.Equal(new[] { "rival", "hero" }, overview.Standings.Select(s => s.Username));
        Assert.Equal(new[] { 1, 2 }, overview.Standings.Select(s => s.Rank));
    }

    [Fact]
    public async Task GetOverview_WithDivisionFilter_ShowsOnlyThatDivision()
    {
        SetParticipants(
            Participant(1, "hero", AmateurId, 1500, 10),
            Participant(3, "newbie", NoviceId, 300, 11));

        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId, NoviceId);

        var overview = result.Value!;
        Assert.Equal(NoviceId, overview.SelectedDivisionId);
        Assert.Equal("Новачок", overview.SelectedDivisionName);
        var only = Assert.Single(overview.Standings);
        Assert.Equal("newbie", only.Username);
        Assert.Equal("Alpha Gym", only.GymName);
    }

    [Fact]
    public async Task GetOverview_StandingsSkipBlockedAndExcludedAthletes()
    {
        SetParticipants(
            Participant(1, "hero", AmateurId, 1500, 10),
            Participant(2, "cheater", AmateurId, 9000, 10, isBlocked: true),
            Participant(3, "hidden", AmateurId, 8000, 10, isExcluded: true));

        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        var only = Assert.Single(result.Value!.Standings);
        Assert.Equal("hero", only.Username);
    }

    [Fact]
    public async Task GetOverview_StandingsShowGymNameAndMarkCurrentUser()
    {
        SetParticipants(
            Participant(1, "hero", AmateurId, 1500, 10),
            Participant(2, "rival", AmateurId, 1200, null));

        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        var standings = result.Value!.Standings;
        Assert.Equal("Iron Temple", standings[0].GymName);
        Assert.True(standings[0].IsCurrentUser);
        Assert.Equal("—", standings[1].GymName);
        Assert.False(standings[1].IsCurrentUser);
    }

    [Fact]
    public async Task GetOverview_WhenXpEqual_OrdersByLevelThenUsername()
    {
        SetParticipants(
            Participant(2, "zeta", AmateurId, 2000, 10),
            Participant(3, "alpha", AmateurId, 2000, 10),
            Participant(4, "leveled", AmateurId, 2000, 10, level: 9));

        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        Assert.Equal(new[] { "leveled", "alpha", "zeta" }, result.Value!.Standings.Select(s => s.Username));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task GetOverview_WhenUserIdNotPositive_ReturnsValidationFailedAndLogsWarning(int userId)
    {
        var result = await _service.GetDivisionQualificationOverviewAsync(userId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Null(result.Value);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        _users.Verify(u => u.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetOverview_WhenDivisionFilterNotPositive_ReturnsValidationFailedAndLogsWarning(int filter)
    {
        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId, filter);

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("divisionIdFilter", result.Field);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        _users.Verify(u => u.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOverview_WhenDivisionFilterDoesNotExist_ReturnsDivisionNotFound()
    {
        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId, 99);

        Assert.Equal(ErrorCodes.DivisionNotFound, result.ErrorCode);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task GetOverview_WhenUserNotFound_ReturnsUserNotFound()
    {
        var result = await _service.GetDivisionQualificationOverviewAsync(77);

        Assert.Equal(ErrorCodes.UserNotFound, result.ErrorCode);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task GetOverview_OnSuccess_LogsInformationAndNoWarning()
    {
        await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information);
        Assert.DoesNotContain(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task GetOverview_WhenDivisionNameUnknown_UsesPositionalThreshold()
    {
        _users
            .Setup(u => u.GetDivisionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Division>
            {
                new() { Id = 1, Name = "Alpha" },
                new() { Id = 2, Name = "Beta" },
            });
        SetUser(divisionId: 1);

        var result = await _service.GetDivisionQualificationOverviewAsync(AthleteId);

        Assert.Equal(new long[] { 0, 1000 }, result.Value!.Divisions.Select(r => r.MinXp));
    }

    [Fact]
    public async Task Promote_WhenXpEnough_UpdatesDivisionKeepingGymAndReturnsResult()
    {
        SetUser(divisionId: AmateurId, gymId: 10);
        SetProgress(AthleteId, 3200, 4);

        var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(AthleteId, AdvancedId));

        Assert.True(result.IsSuccess);
        var value = result.Value!;
        Assert.Equal(AmateurId, value.PreviousDivisionId);
        Assert.Equal("Любитель", value.PreviousDivisionName);
        Assert.Equal(AdvancedId, value.NewDivisionId);
        Assert.Equal("Просунутий", value.NewDivisionName);
        Assert.Equal(3200, value.CurrentXp);
        Assert.Equal(4, value.CurrentLevel);
        _users.Verify(
            u => u.UpdateAffiliationAsync(AthleteId, "hero", 10, AdvancedId, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information);
        Assert.DoesNotContain(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Promote_WhenXpExactlyEqualsThreshold_Succeeds()
    {
        SetProgress(AthleteId, 3000, 4);

        var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(AthleteId, AdvancedId));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Promote_WhenUserHasNoDivision_AllowsFirstDivision()
    {
        SetUser(divisionId: null);
        SetProgress(AthleteId, 0, 1);

        var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(AthleteId, NoviceId));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.PreviousDivisionId);
        Assert.Equal("—", result.Value.PreviousDivisionName);
    }

    [Fact]
    public async Task Promote_WhenAthleteSkipsDivisionsWithEnoughXp_Succeeds()
    {
        SetProgress(AthleteId, 6500, 7);

        var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(AthleteId, ProId));

        Assert.True(result.IsSuccess);
        Assert.Equal(ProId, result.Value!.NewDivisionId);
    }

    [Fact]
    public async Task Promote_WhenNotEnoughXp_ReturnsValidationFailedWithMissingXp()
    {
        SetProgress(AthleteId, 2500, 3);

        var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(AthleteId, AdvancedId));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(PromoteDivisionRequest.TargetDivisionId), result.Field);
        Assert.Contains("500", result.Message!);
        Assert.Contains("Просунутий", result.Message!);
        Assert.Null(result.Value);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        VerifyNoUpdate();
    }

    [Fact]
    public async Task Promote_WhenProgressMissing_TreatsXpAsZero()
    {
        _progress
            .Setup(p => p.GetByUserIdAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProgress?)null);

        var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(AthleteId, AdvancedId));

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Contains("3000", result.Message!);
        VerifyNoUpdate();
    }

    [Fact]
    public async Task Promote_WhenUserBlocked_ReturnsModerationAccessDenied()
    {
        SetUser(divisionId: AmateurId, isBlocked: true);
        SetProgress(AthleteId, 9000, 10);

        var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(AthleteId, AdvancedId));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ModerationAccessDenied, result.ErrorCode);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        VerifyNoUpdate();
    }

    [Fact]
    public async Task Promote_WhenUserExcludedFromLeaderboard_ReturnsModerationAccessDenied()
    {
        SetUser(divisionId: AmateurId, isExcluded: true);
        SetProgress(AthleteId, 9000, 10);

        var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(AthleteId, AdvancedId));

        Assert.Equal(ErrorCodes.ModerationAccessDenied, result.ErrorCode);
        VerifyNoUpdate();
    }

    [Theory]
    [InlineData(AmateurId)]
    [InlineData(NoviceId)]
    public async Task Promote_WhenTargetIsNotHigherThanCurrent_ReturnsValidationFailed(int targetId)
    {
        SetProgress(AthleteId, 9000, 10);

        var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(AthleteId, targetId));

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(PromoteDivisionRequest.TargetDivisionId), result.Field);
        VerifyNoUpdate();
    }

    [Fact]
    public async Task Promote_WhenTargetDivisionDoesNotExist_ReturnsDivisionNotFound()
    {
        var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(AthleteId, 99));

        Assert.Equal(ErrorCodes.DivisionNotFound, result.ErrorCode);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        VerifyNoUpdate();
    }

    [Fact]
    public async Task Promote_WhenUserNotFound_ReturnsUserNotFound()
    {
        var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(77, AdvancedId));

        Assert.Equal(ErrorCodes.UserNotFound, result.ErrorCode);
        VerifyNoUpdate();
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(-1, 3)]
    [InlineData(1, 0)]
    [InlineData(1, -2)]
    public async Task Promote_WhenIdsNotPositive_ReturnsValidationFailed(int userId, int divisionId)
    {
        var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(userId, divisionId));

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        _users.Verify(u => u.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoUpdate();
    }

    [Fact]
    public async Task Promote_WhenRequestIsNull_ReturnsValidationFailed()
    {
        var result = await _service.PromoteToDivisionAsync(null!);

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("request", result.Field);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Promote_WhenRepositoryCannotSave_ReturnsFailure()
    {
        SetProgress(AthleteId, 3500, 4);
        _users
            .Setup(u => u.UpdateAffiliationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(AthleteId, AdvancedId));

        Assert.False(result.IsSuccess);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.DoesNotContain(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information);
    }

    private static List<Division> CreateDivisions()
    {
        return new List<Division>
        {
            new() { Id = EliteId, Name = "Еліта" },
            new() { Id = NoviceId, Name = "Новачок" },
            new() { Id = AmateurId, Name = "Любитель" },
            new() { Id = ProId, Name = "Професіонал" },
            new() { Id = AdvancedId, Name = "Просунутий" },
        };
    }

    private static User Participant(
        int id,
        string name,
        int divisionId,
        long xp,
        int? gymId,
        bool isBlocked = false,
        bool isExcluded = false,
        int level = 1)
    {
        return new User
        {
            Id = id,
            Username = name,
            DivisionId = divisionId,
            GymId = gymId,
            IsBlocked = isBlocked,
            IsExcludedFromLeaderboard = isExcluded,
            UserProgress = new UserProgress { UserId = id, Xp = xp, Level = level },
        };
    }

    private void SetUser(int? divisionId, int? gymId = null, bool isBlocked = false, bool isExcluded = false)
    {
        _users
            .Setup(u => u.GetByIdAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = AthleteId,
                Username = "hero",
                DivisionId = divisionId,
                GymId = gymId,
                IsBlocked = isBlocked,
                IsExcludedFromLeaderboard = isExcluded,
            });
    }

    private void SetProgress(int userId, long xp, int level)
    {
        _progress
            .Setup(p => p.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProgress { UserId = userId, Xp = xp, Level = level });
    }

    private void SetParticipants(params User[] users)
    {
        _progress
            .Setup(p => p.GetLeaderboardUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users.ToList());
    }

    private void VerifyNoUpdate()
    {
        _users.Verify(
            u => u.UpdateAffiliationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
