namespace Handball.Belgium.RefTestManagement.Application.Configurations;

public sealed class StateConfiguration
{
    public StateMode Mode { get; init; } = StateMode.Local;

    public void Validate(RedisConfiguration redis, PrivacyChallengeConfiguration privacyChallenge)
    {
        if (!Enum.IsDefined(Mode))
            throw new InvalidOperationException("State:Mode must be Local or Shared.");

        if (Mode == StateMode.Shared && !redis.IsConfigured)
            throw new InvalidOperationException(
                "State:Mode=Shared requires RedisConfiguration:Endpoint.");

        if (Mode == StateMode.Shared &&
            privacyChallenge.RateLimitBackend != PrivacyChallengeRateLimitBackend.Redis)
            throw new InvalidOperationException(
                "State:Mode=Shared requires PrivacyChallengeConfiguration:RateLimitBackend=Redis.");
    }
}

public enum StateMode
{
    Local,
    Shared
}
