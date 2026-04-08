using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FreeGLBA.Client;

/// <summary>
/// Extension methods for registering GLBA client services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the GLBA client to the service collection with the specified configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the client options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddGlbaClient(options =>
    /// {
    ///     options.Endpoint = "https://glba.example.com";
    ///     options.ApiKey = builder.Configuration["GlbaApiKey"];
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddGlbaClient(
        this IServiceCollection services,
        Action<GlbaClientOptions> configure)
    {
        services.Configure(configure);

        services.AddHttpClient<IGlbaClient, GlbaClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<GlbaClientOptions>>().Value;
            options.Validate();

            client.BaseAddress = new Uri(options.Endpoint.TrimEnd('/') + "/");
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }

    /// <summary>
    /// Adds the GLBA client to the service collection with the specified endpoint and API key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="endpoint">The base URL of the FreeGLBA server.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddGlbaClient(
    ///     "https://glba.example.com",
    ///     builder.Configuration["GlbaApiKey"]!
    /// );
    /// </code>
    /// </example>
    public static IServiceCollection AddGlbaClient(
        this IServiceCollection services,
        string endpoint,
        string apiKey)
    {
        return services.AddGlbaClient(options =>
        {
            options.Endpoint = endpoint;
            options.ApiKey = apiKey;
        });
    }

    /// <summary>
    /// Adds the GLBA client to the service collection with custom HTTP client configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the client options.</param>
    /// <param name="configureHttpClient">Action to configure the HTTP client.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddGlbaClient(
    ///     options =>
    ///     {
    ///         options.Endpoint = "https://glba.example.com";
    ///         options.ApiKey = builder.Configuration["GlbaApiKey"];
    ///     },
    ///     client =>
    ///     {
    ///         client.DefaultRequestHeaders.Add("X-Custom-Header", "value");
    ///     }
    /// );
    /// </code>
    /// </example>
    public static IServiceCollection AddGlbaClient(
        this IServiceCollection services,
        Action<GlbaClientOptions> configure,
        Action<HttpClient>? configureHttpClient)
    {
        services.Configure(configure);

        services.AddHttpClient<IGlbaClient, GlbaClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<GlbaClientOptions>>().Value;
            options.Validate();

            client.BaseAddress = new Uri(options.Endpoint.TrimEnd('/') + "/");
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            configureHttpClient?.Invoke(client);
        });

        return services;
    }
}
