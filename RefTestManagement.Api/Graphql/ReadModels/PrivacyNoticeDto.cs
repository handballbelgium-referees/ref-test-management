namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

public sealed record PrivacyNoticeDto(
    string ControllerName,
    string ControllerAddress,
    string ContactEmail,
    string NoticeVersion,
    DateOnly NoticeEffectiveDate,
    int RetentionYears);