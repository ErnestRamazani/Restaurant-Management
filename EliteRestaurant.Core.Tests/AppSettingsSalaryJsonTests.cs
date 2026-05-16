using System.Text.Json;
using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Core.Tests;

public sealed class AppSettingsSalaryJsonTests
{
    private static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Deserialize_partial_json_without_salary_property_keeps_initializer_defaults()
    {
        const string json = """{"BusinessProfile":{"RestaurantName":"X"},"CurrencyPricing":{}}""";
        var loaded = JsonSerializer.Deserialize<AppSettings>(json, CaseInsensitive) ?? new AppSettings();
        Assert.NotNull(loaded.Salary);
        Assert.Equal(4, loaded.Salary.LateDaysPerAttendanceUnit);
        Assert.Equal(5m, loaded.Salary.SalesBonusPercent);
    }

    [Fact]
    public void RoundTrip_salary_pascal_case_json()
    {
        var original = new AppSettings();
        original.Salary.LateDaysPerAttendanceUnit = 7;
        original.Salary.AbsenceCountsAsAttendanceUnit = false;
        original.Salary.SalesBonusPercent = 12.5m;
        original.Salary.MaxSalaryAdvancePercentOfGross = 40m;

        var json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
        var roundTrip = JsonSerializer.Deserialize<AppSettings>(json, CaseInsensitive) ?? new AppSettings();

        Assert.Equal(7, roundTrip.Salary.LateDaysPerAttendanceUnit);
        Assert.False(roundTrip.Salary.AbsenceCountsAsAttendanceUnit);
        Assert.Equal(12.5m, roundTrip.Salary.SalesBonusPercent);
        Assert.Equal(40m, roundTrip.Salary.MaxSalaryAdvancePercentOfGross);
    }
}
