using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Data;

// Довідники регіонів, спортзалів і дивізіонів: початкове наповнення БД та резерв на випадок, коли PostgreSQL недоступний.
internal static class AffiliationSeedData
{
    private static readonly string[] RegionNames =
    {
        "Київ",
        "Львівська область",
        "Одеська область",
        "Харківська область",
        "Дніпропетровська область",
    };

    private static readonly (string Region, string Name)[] GymSeeds =
    {
        ("Київ", "PulseForge Kyiv Center"),
        ("Київ", "Iron Temple Київ"),
        ("Львівська область", "Львів Фітнес Арена"),
        ("Львівська область", "Gym Lviv Strong"),
        ("Одеська область", "Одеса Power Gym"),
        ("Одеська область", "Black Sea Fitness"),
        ("Харківська область", "Харків Iron Club"),
        ("Харківська область", "Kharkiv Athletic"),
        ("Дніпропетровська область", "Дніпро Muscle House"),
        ("Дніпропетровська область", "Dnipro Pulse Gym"),
    };

    private static readonly string[] DivisionNames =
    {
        "Новачок",
        "Любитель",
        "Просунутий",
        "Професіонал",
        "Еліта",
    };

    // Регіони з вкладеними залами для запису в БД: ідентифікатори й зовнішні ключі виставляє EF.
    public static IReadOnlyList<Region> CreateRegionGraph()
    {
        return RegionNames
            .Select(regionName => new Region
            {
                Name = regionName,
                Gyms = GymSeeds
                    .Where(gym => gym.Region == regionName)
                    .Select(gym => new Gym { Name = gym.Name })
                    .ToList(),
            })
            .ToList();
    }

    public static IReadOnlyList<Division> CreateDivisions()
    {
        return DivisionNames
            .Select((name, index) => new Division { Id = index + 1, Name = name })
            .ToList();
    }

    // Резервні довідники з фіксованими Id (1..N у порядку оголошення).
    public static IReadOnlyList<Region> CreateFallbackRegions()
    {
        return RegionNames
            .Select((name, index) => new Region { Id = index + 1, Name = name })
            .ToList();
    }

    public static IReadOnlyList<Gym> CreateFallbackGyms()
    {
        return GymSeeds
            .Select((gym, index) => new Gym
            {
                Id = index + 1,
                RegionId = Array.IndexOf(RegionNames, gym.Region) + 1,
                Name = gym.Name,
            })
            .ToList();
    }
}
