namespace Handball.Belgium.RefTestManagement.Application.Configurations;

public class PrivacyConfiguration
{
    public string ControllerName { get; init; } = "Kristof Gilis";
    public string ControllerAddress { get; init; } = "Pipelstraat 26, 3800 Sint-Truiden, Belgium";
    public string ContactEmail { get; init; } = "kristof.gilis@outlook.be";
    public string NoticeVersion { get; init; } = "1.0";
    public DateOnly NoticeEffectiveDate { get; init; } = new(2026, 8, 3);
    public int RetentionYears { get; init; } = 3;
}