using System.ComponentModel.DataAnnotations;

namespace Handball.Belgium.RefTestManagement.Api.Configurations;

public static class ConfigurationServiceCollectionExtensions
{
    /// <summary>
    /// Binds <paramref name="sectionName"/>, validates it and registers it as a singleton.
    /// Validation runs the type's data annotations, then <paramref name="validate"/>, and fails
    /// startup with an <see cref="InvalidOperationException"/> naming the section, so a bad value
    /// never reaches a running service.
    /// </summary>
    /// <returns>The bound value, for startup code that composes services from it.</returns>
    public static T AddValidatedConfiguration<T>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        Action<T>? validate = null)
        where T : class, new()
    {
        var value = configuration.GetSection(sectionName).Get<T>() ?? new T();

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true))
            throw new InvalidOperationException(
                $"{sectionName} contains invalid values: {string.Join(" ", results.Select(r => r.ErrorMessage))}");

        validate?.Invoke(value);

        services.AddSingleton(value);
        return value;
    }
}
