using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;


namespace FreeCICD.EFModels.EFModels
{
    /// <summary>
    /// Design-time factory for EFDataModel used by EF Core tools (Add-Migration, Update-Database).
    /// The factory prefers the following sources for the connection string (in order):
    /// 1. Environment variable "EF_CONNECTIONSTRING"
    /// 2. Configuration ConnectionStrings:AppData from appsettings.migrations.json (migrations-specific)
    /// 3. Configuration ConnectionStrings:AppData from appsettings.json (or appsettings.{ENV}.json)
    /// 4. Environment variable "ConnectionStrings__AppData"
    ///
    /// This factory is only used by the EF tooling and will not affect runtime DI-based DbContext creation.
    /// </summary>
    public class EFDataModelFactory : IDesignTimeDbContextFactory<EFDataModel>
    {
        public EFDataModel CreateDbContext(string[] args)
        {
            // 1) Check explicit environment variable
            var conn = Environment.GetEnvironmentVariable("EF_CONNECTIONSTRING");
            if (!string.IsNullOrWhiteSpace(conn))
            {
                return Create(conn);
            }

            // 2) Build configuration from appsettings files and environment
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            // Determine a base path for configuration. EF tooling usually runs from the project directory
            // but in some environments CurrentDirectory may be different. Search upward for appsettings.json
            string basePath = Directory.GetCurrentDirectory();
            string configFile = Path.Combine(basePath, "appsettings.json");
            string migrationsConfigFile = Path.Combine(basePath, "appsettings.migrations.json");

            int climbs = 0;
            while (!File.Exists(configFile) && !File.Exists(migrationsConfigFile) && climbs < 10)
            {
                var parent = Directory.GetParent(basePath);
                if (parent == null) break;
                basePath = parent.FullName;
                configFile = Path.Combine(basePath, "appsettings.json");
                migrationsConfigFile = Path.Combine(basePath, "appsettings.migrations.json");
                climbs++;
            }

            // If still not found, fall back to AppContext.BaseDirectory which is common for tooling
            if (!File.Exists(configFile) && !File.Exists(migrationsConfigFile))
            {
                basePath = AppContext.BaseDirectory;
            }

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(basePath);

            // Add migrations-specific config first (highest priority)
            configBuilder.AddJsonFile("appsettings.migrations.json", optional: true, reloadOnChange: false);

            // Then add regular appsettings
            configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            configBuilder.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);

            // Environment variables last
            configBuilder.AddEnvironmentVariables();

            var config = configBuilder.Build();

            // Try common keys used in this repository
            conn = config.GetConnectionString("AppData");
            if (string.IsNullOrWhiteSpace(conn))
            {
                conn = config["ConnectionStrings:AppData"]; // explicit key
            }

            // 3) Fallback to environment-style key
            if (string.IsNullOrWhiteSpace(conn))
            {
                conn = Environment.GetEnvironmentVariable("ConnectionStrings__AppData");
            }

            if (string.IsNullOrWhiteSpace(conn))
            {
                throw new InvalidOperationException(
                    "A connection string for EF migrations was not found. Set the environment variable 'EF_CONNECTIONSTRING' or add 'ConnectionStrings:AppData' to appsettings.migrations.json or appsettings.json."
                );
            }

            return Create(conn);
        }

        private static EFDataModel Create(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<EFDataModel>();

            // Use SQL Server by default and enable retry on failure for reliability
            optionsBuilder.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());

            return new EFDataModel(optionsBuilder.Options);
        }
    }
}
