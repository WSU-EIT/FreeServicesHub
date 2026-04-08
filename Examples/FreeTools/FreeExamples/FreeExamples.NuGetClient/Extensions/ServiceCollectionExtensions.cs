using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FreeExamples.Client;

/// <summary>
/// Extension methods for registering the FreeExamples client with DI.
/// Pattern from: FreeGLBA.Client.ServiceCollectionExtensions
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the FreeExamples client to the service collection with the specified configuration.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddFreeExamplesClient(options =>
    /// {
    ///     options.Endpoint = "https://localhost:7271";
    ///     options.ApiKey = builder.Configuration["FreeExamples:ApiKey"]!;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddFreeExamplesClient(
        this IServiceCollection services,
        Action<FreeExamplesClientOptions> configure)
    {
        services.Configure(configure);

        services.AddHttpClient<IFreeExamplesClient, FreeExamplesClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<FreeExamplesClientOptions>>().Value;
            options.Validate();

            client.BaseAddress = new Uri(options.Endpoint.TrimEnd('/') + "/");
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }

    /// <summary>
    /// Adds the FreeExamples client with endpoint and API key directly.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddFreeExamplesClient(
    ///     "https://localhost:7271",
    ///     builder.Configuration["FreeExamples:ApiKey"]!
    /// );
    /// </code>
    /// </example>
    public static IServiceCollection AddFreeExamplesClient(
        this IServiceCollection services,
        string endpoint,
        string apiKey)
    {
        return services.AddFreeExamplesClient(options =>
        {
            options.Endpoint = endpoint;
            options.ApiKey = apiKey;
        });
    }
}
