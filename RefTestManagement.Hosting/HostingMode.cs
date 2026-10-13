namespace Handball.Belgium.RefTestManagement.Hosting;

public enum HostingMode
{
    All,
    Api,
    Worker
}

public static class HostingModeConfiguration
{
    public static HostingMode GetApiMode(IConfiguration configuration)
    {
        var mode = Parse(configuration["Hosting:Mode"], HostingMode.All);
        if (mode == HostingMode.Worker)
            throw new InvalidOperationException(
                "Hosting:Mode=Worker is not valid for the API executable. Use All or Api.");

        return mode;
    }

    public static HostingMode GetWorkerMode(IConfiguration configuration)
    {
        var mode = Parse(configuration["Hosting:Mode"], HostingMode.Worker);
        if (mode != HostingMode.Worker)
            throw new InvalidOperationException(
                "The Worker executable requires Hosting:Mode=Worker.");

        return mode;
    }

    private static HostingMode Parse(string? value, HostingMode defaultMode)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultMode;

        if (Enum.TryParse(value, ignoreCase: true, out HostingMode mode) &&
            Enum.IsDefined(mode) &&
            string.Equals(value, mode.ToString(), StringComparison.OrdinalIgnoreCase))
            return mode;

        throw new InvalidOperationException(
            $"Invalid Hosting:Mode '{value}'. Valid values: All, Api, Worker.");
    }
}
