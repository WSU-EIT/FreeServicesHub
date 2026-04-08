// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  FreeExamples.DatabaseMigration — Same-Schema Database Copy/Migration Tool  ║
// ╠══════════════════════════════════════════════════════════════════════════════╣
// ║                                                                              ║
// ║  PURPOSE:                                                                    ║
// ║    Copy data from one SQL Server database to another with identical schema.   ║
// ║    Uses SqlBulkCopy for high-speed transfers with live progress reporting.    ║
// ║                                                                              ║
// ║  WHAT THIS DEMONSTRATES:                                                     ║
// ║    This is an open-source EXAMPLE of a pattern used across multiple          ║
// ║    production projects. It shows how to build a safe, interactive database    ║
// ║    migration tool with:                                                       ║
// ║      • Dry-run mode (default) — preview without writing                      ║
// ║      • Phased migration — tables grouped by FK dependency order              ║
// ║      • Generic table copier — one method handles ANY table                   ║
// ║      • Live progress — rate, ETA, batch counters                             ║
// ║      • Data integrity verification — CSV export + SHA256 comparison          ║
// ║      • Column profiling — schema analysis (min/max/nulls)                    ║
// ║      • Full logging — timestamped .log files in runs/ folder                 ║
// ║      • CLI automation — run headless via appsettings or command-line args    ║
// ║                                                                              ║
// ║  BASED ON:                                                                   ║
// ║    Pattern from Touchpoints.DatabaseImportV2 (same-schema bulk copy) with    ║
// ║    features from Touchpoints.DatabaseImport (integrity verification, column  ║
// ║    profiling) and AcademicCalendarPetitions.MigrationTool (appsettings.json   ║
// ║    configuration). See docs/300_research.migration_tools.md for analysis.     ║
// ║                                                                              ║
// ║  HOW TO REPURPOSE:                                                           ║
// ║    1. Update appsettings.json with your connection strings (or use secrets)   ║
// ║    2. Update the Phase arrays in appsettings.json with your table names      ║
// ║    3. Tables are copied generically — no entity-specific code needed         ║
// ║    4. For CROSS-SCHEMA migration (different column names/types), see the     ║
// ║       "TRANSFORM HOOK" comments in MigrateTable() for where to add mapping   ║
// ║    5. For ID TYPE CHANGES (e.g., int → GUID), see the "ID TRANSFORM"        ║
// ║       comments showing how to add a MigrationTracking table                  ║
// ║                                                                              ║
// ║  USAGE:                                                                      ║
// ║    Interactive:   dotnet run                                                  ║
// ║    CLI verify:    dotnet run -- --Migration:AutoRun=verify                    ║
// ║    CLI full run:  dotnet run -- --Migration:AutoRun=all                       ║
// ║                     --Migration:DryRunOnStart=false                           ║
// ║                     --Migration:AutoConfirm=true                              ║
// ║    Phase only:    dotnet run -- --Migration:AutoRun=phaseA                    ║
// ║    Override DB:   dotnet run -- --Migration:SourceDb="Data Source=..."        ║
// ║                                                                              ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace FreeExamples.DatabaseMigration;

public class Program
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Configuration (loaded from appsettings.json + user secrets + CLI args)
    // ═══════════════════════════════════════════════════════════════════════════
    private static MigrationConfig _config = new();

    // State
    private static bool _dryRun = true;
    private static string _runsDirectory = null!;
    private static string _logFilePath = null!;
    private static StreamWriter? _logWriter;
    private static DateTime _runStartTime;

    // ═══════════════════════════════════════════════════════════════════════════

    public static async Task<int> Main(string[] args)
    {
        // Load configuration from appsettings.json, user secrets, and CLI args
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddUserSecrets<Program>(optional: true)
            .AddCommandLine(args)
            .Build();

        configuration.GetSection("Migration").Bind(_config);
        _dryRun = _config.DryRunOnStart;

        InitializeLogging();

        Log("╔══════════════════════════════════════════════════════════════════╗");
        Log("║     FreeExamples — Database Migration / Copy Tool                ║");
        Log("╚══════════════════════════════════════════════════════════════════╝");
        Log("");

        try {
            // Display connection info
            Log("═══════════════════════════════════════════════════════════════");
            Log("                    CONNECTION STRINGS                         ");
            Log("═══════════════════════════════════════════════════════════════");
            Log($"  Source: {MaskConnectionString(_config.SourceDb)}");
            Log($"  Target: {MaskConnectionString(_config.TargetDb)}");
            Log("");

            // Test connectivity
            Log("═══════════════════════════════════════════════════════════════");
            Log("                    DATABASE CONNECTIVITY                      ");
            Log("═══════════════════════════════════════════════════════════════");
            await TestConnectivity("Source", _config.SourceDb);
            await TestConnectivity("Target", _config.TargetDb);
            Log("");

            // Display phase configuration
            Log("═══════════════════════════════════════════════════════════════");
            Log("                    PHASE CONFIGURATION                        ");
            Log("═══════════════════════════════════════════════════════════════");
            Log($"  Phase A ({_config.PhaseA.Length} tables): {string.Join(", ", _config.PhaseA)}");
            Log($"  Phase B ({_config.PhaseB.Length} tables): {string.Join(", ", _config.PhaseB)}");
            Log($"  Phase C ({_config.PhaseC.Length} tables): {string.Join(", ", _config.PhaseC)}");
            Log($"  Total: {AllTablesOrdered.Length} tables");
            Log("");

            // ═══════════════════════════════════════════════════════════════
            // CLI AUTOMATION MODE
            // If AutoRun is set, run the specified command and exit.
            // ═══════════════════════════════════════════════════════════════
            if (!string.IsNullOrWhiteSpace(_config.AutoRun)) {
                Log($"  AutoRun: {_config.AutoRun} (DryRun={_dryRun}, AutoConfirm={_config.AutoConfirm})");
                Log("");

                int exitCode = await RunAutoCommand(_config.AutoRun);

                FinalizeLogging();
                return exitCode;
            }

            // ═══════════════════════════════════════════════════════════════
            // INTERACTIVE MENU
            // ═══════════════════════════════════════════════════════════════
            while (true) {
                Log("");
                Log("═══════════════════════════════════════════════════════════════");
                DisplayModeHeader();
                Log("═══════════════════════════════════════════════════════════════");
                Log("  0. Verify databases (compare counts) - READ ONLY");
                Log("  ─────────────────────────────────────────────────────────────");
                Log("  A. Phase A: FOUNDATION (Tenants, PluginCaches)");
                Log("  B. Phase B: USERS & CONFIG (Depts, Users, Settings…)");
                Log("  C. Phase C: APPLICATION DATA (Tags, TagItems, FileStorage)");
                Log("  ─────────────────────────────────────────────────────────────");
                Log("  9. Full migration (All phases A → C in order)");
                Log("  ─────────────────────────────────────────────────────────────");
                Log("  X. TRUNCATE all target tables (⚠️  deletes all data!)");
                Log("  P. Column profiling (schema analysis) - READ ONLY");
                Log("  I. Data integrity verification (CSV + SHA256) - READ ONLY");
                Log("  V. Preview sample data - READ ONLY");
                Log("  ─────────────────────────────────────────────────────────────");
                Log("  E. Create target DB (fresh EF migration → apply to target)");
                Log("  M. Generate fresh EF migration files only");
                Log("  U. Apply existing EF migration to target DB");
                Log("");
                Log("  D. Toggle DRY RUN mode");
                Log("  Q. Exit");
                Log("");
                Console.Write("Select option: ");

                var key = Console.ReadKey();
                Log("", logOnly: true);
                Log($"Selected: {key.KeyChar}", logOnly: true);
                Console.WriteLine();
                Console.WriteLine();

                switch (char.ToUpper(key.KeyChar)) {
                    case '0': await RunVerification(); break;
                    case 'A': await ImportPhase("A", "FOUNDATION", _config.PhaseA); break;
                    case 'B': await ImportPhase("B", "USERS & CONFIG", _config.PhaseB); break;
                    case 'C': await ImportPhase("C", "APPLICATION DATA", _config.PhaseC); break;
                    case '9': await RunAllPhases(); break;
                    case 'X': await TruncateAllTables(); break;
                    case 'P': await RunColumnProfiling(); break;
                    case 'I': await RunDataIntegrityVerification(); break;
                    case 'V': await PreviewSampleData(); break;
                    case 'E': await CreateTargetDatabase(); break;
                    case 'M': await GenerateFreshMigration(); break;
                    case 'U': await ApplyMigrationToTarget(); break;
                    case 'D': ToggleDryRunMode(); break;
                    case 'Q':
                        Log("Exiting...");
                        FinalizeLogging();
                        return 0;
                    default: Log("Invalid option. Please try again."); break;
                }
            }
        } catch (Exception ex) {
            LogError("Unhandled Exception", ex.Message, ex.InnerException?.Message);
            Log($"Stack Trace: {ex.StackTrace}", logOnly: true);
        }

        FinalizeLogging();
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        return 1;
    }

    // All tables in FK dependency order (built from phase arrays)
    private static string[] AllTablesOrdered => [.. _config.PhaseA, .. _config.PhaseB, .. _config.PhaseC];

    #region CLI Automation

    /// <summary>
    /// Run a single command by name and return an exit code.
    /// Called when appsettings "AutoRun" is set or via CLI args.
    /// </summary>
    private static async Task<int> RunAutoCommand(string command)
    {
        try {
            switch (command.ToLower()) {
                case "verify":
                    await RunVerification();
                    break;
                case "all":
                    await RunAllPhases();
                    break;
                case "phasea":
                    await ImportPhase("A", "FOUNDATION", _config.PhaseA);
                    break;
                case "phaseb":
                    await ImportPhase("B", "USERS & CONFIG", _config.PhaseB);
                    break;
                case "phasec":
                    await ImportPhase("C", "APPLICATION DATA", _config.PhaseC);
                    break;
                case "truncate":
                    await TruncateAllTables();
                    break;
                case "profile":
                    await RunColumnProfiling();
                    break;
                case "integrity":
                    await RunDataIntegrityVerification();
                    break;
                case "createdb":
                    await CreateTargetDatabase();
                    break;
                case "efmigrate":
                    await GenerateFreshMigration();
                    break;
                case "efupdate":
                    await ApplyMigrationToTarget();
                    break;
                default:
                    LogError("AutoRun", $"Unknown command: '{command}'");
                    Log("  Valid commands: verify, all, phaseA, phaseB, phaseC, truncate, profile, integrity, createdb, efmigrate, efupdate");
                    return 1;
            }
            return 0;
        } catch (Exception ex) {
            LogError("AutoRun Failed", ex.Message, ex.InnerException?.Message);
            return 1;
        }
    }

    #endregion

    #region Logging

    private static void InitializeLogging()
    {
        _runStartTime = DateTime.Now;
        string sourceDir = FindProjectSourceDirectory();
        _runsDirectory = Path.Combine(sourceDir, "runs");

        // Clean previous run logs
        if (Directory.Exists(_runsDirectory)) {
            foreach (string file in Directory.GetFiles(_runsDirectory, "*.log")) {
                try { File.Delete(file); } catch { }
            }
        } else {
            Directory.CreateDirectory(_runsDirectory);
        }

        _logFilePath = Path.Combine(_runsDirectory, $"migration-{_runStartTime:yyyyMMdd-HHmmss}.log");
        _logWriter = new StreamWriter(_logFilePath, append: false) { AutoFlush = true };

        _logWriter.WriteLine($"FreeExamples Database Migration Log");
        _logWriter.WriteLine($"Started: {_runStartTime:yyyy-MM-dd HH:mm:ss}");
        _logWriter.WriteLine($"{"".PadRight(63, '=')}");
        _logWriter.WriteLine();
    }

    private static string FindProjectSourceDirectory()
    {
        string currentDir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++) {
            if (File.Exists(Path.Combine(currentDir, "FreeExamples.DatabaseMigration.csproj")) ||
                File.Exists(Path.Combine(currentDir, "Program.cs")))
                return currentDir;

            DirectoryInfo? parent = Directory.GetParent(currentDir);
            if (parent == null) break;
            currentDir = parent.FullName;
        }
        return Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? ".";
    }

    private static void FinalizeLogging()
    {
        if (_logWriter != null) {
            TimeSpan duration = DateTime.Now - _runStartTime;
            _logWriter.WriteLine();
            _logWriter.WriteLine($"{"".PadRight(63, '=')}");
            _logWriter.WriteLine($"Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _logWriter.WriteLine($"Duration: {duration.TotalSeconds:F1} seconds");
            _logWriter.Close();
            _logWriter = null;
            Log($"Log saved to: {_logFilePath}");
        }
    }

    private static void Log(string message, bool logOnly = false)
    {
        if (!logOnly) Console.WriteLine(message);
        _logWriter?.WriteLine(message);
    }

    private static void LogColored(string message, ConsoleColor color, bool logOnly = false)
    {
        if (!logOnly) {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        _logWriter?.WriteLine(message);
    }

    private static void LogWrite(string message)
    {
        Console.Write(message);
        _logWriter?.Write(message);
    }

    private static void LogError(string title, string message, string? innerMessage = null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"ERROR: {title}");
        Console.WriteLine($"  {message}");
        if (!string.IsNullOrEmpty(innerMessage)) Console.WriteLine($"  Inner: {innerMessage}");
        Console.ResetColor();
        _logWriter?.WriteLine($"ERROR: {title} - {message}");
        if (!string.IsNullOrEmpty(innerMessage)) _logWriter?.WriteLine($"  Inner: {innerMessage}");
    }

    #endregion

    #region UI Helpers

    private static void DisplayModeHeader()
    {
        if (_dryRun)
            LogColored("              MENU - 🔒 DRY RUN MODE (No writes)              ", ConsoleColor.Cyan);
        else
            LogColored("              MENU - ⚠️  LIVE MODE (Will write data!)          ", ConsoleColor.Red);
    }

    private static void ToggleDryRunMode()
    {
        _dryRun = !_dryRun;
        Log("");
        if (_dryRun) {
            LogColored("🔒 DRY RUN MODE ENABLED - No data will be written to the database.", ConsoleColor.Cyan);
        } else {
            LogColored("⚠️  LIVE MODE ENABLED - Data WILL be written to the database!", ConsoleColor.Red);
            LogColored("   Press 'D' again to switch back to Dry Run.", ConsoleColor.Red);
        }
    }

    private static string MaskConnectionString(string cs)
    {
        try {
            SqlConnectionStringBuilder builder = new(cs);
            if (!string.IsNullOrEmpty(builder.Password)) builder.Password = "****";
            return builder.ConnectionString;
        } catch {
            return "(invalid connection string)";
        }
    }

    private static async Task TestConnectivity(string name, string cs)
    {
        LogWrite($"  {name}: ");
        try {
            await using SqlConnection conn = new(cs);
            await conn.OpenAsync();
            LogColored("Connected ✓", ConsoleColor.Green);
        } catch (Exception ex) {
            LogColored($"FAILED - {ex.Message}", ConsoleColor.Red);
        }
    }

    private static void PrintMigrationResult(int migrated, int skipped)
    {
        string msg = $"    ✓ {(_dryRun ? "Would migrate" : "Migrated")}: {migrated:N0}, {(_dryRun ? "Would skip" : "Skipped")}: {skipped:N0}";
        LogColored(msg, _dryRun ? ConsoleColor.Cyan : ConsoleColor.Green);
    }

    /// <summary>
    /// Prompt for confirmation. Returns true if confirmed.
    /// In AutoConfirm mode, always returns true without prompting.
    /// </summary>
    private static bool ConfirmAction(string actionDescription)
    {
        if (_config.AutoConfirm) {
            Log($"  [AutoConfirm] {actionDescription}");
            return true;
        }

        Console.Write($"  Type 'YES' to confirm {actionDescription}: ");
        string? confirm = Console.ReadLine();
        if (confirm != "YES") {
            Log("  Cancelled.");
            return false;
        }
        return true;
    }

    #endregion

    #region Verification

    private static async Task RunVerification()
    {
        Log("═══════════════════════════════════════════════════════════════");
        Log("            DATABASE VERIFICATION (Source vs Target)            ");
        Log("═══════════════════════════════════════════════════════════════");
        Log("");

        Log("  ┌──────────────────────────────┬────────────┬────────────┬──────────┐");
        Log("  │ Table Name                   │ Source     │ Target     │ Status   │");
        Log("  ├──────────────────────────────┼────────────┼────────────┼──────────┤");

        foreach (string table in AllTablesOrdered) {
            int sourceCount = await GetTableCountAsync(_config.SourceDb, table);
            int targetCount = await GetTableCountAsync(_config.TargetDb, table);

            string status = (sourceCount, targetCount) switch {
                (-1, -1) => "MISSING",
                (-1, _) => "NO SRC",
                (_, -1) => "NO TGT",
                var (s, t) when s == t && s > 0 => "MATCH",
                var (s, t) when s == t && s == 0 => "EMPTY",
                var (_, t) when t == 0 => "PENDING",
                var (s, t) when t < s => "PARTIAL",
                _ => "EXTRA"
            };

            ConsoleColor color = status switch {
                "MATCH" => ConsoleColor.Green,
                "PENDING" => ConsoleColor.Yellow,
                "PARTIAL" => ConsoleColor.Cyan,
                "EXTRA" => ConsoleColor.Magenta,
                "EMPTY" => ConsoleColor.DarkGray,
                _ => ConsoleColor.DarkGray
            };

            string srcStr = sourceCount < 0 ? "N/A" : sourceCount.ToString("N0");
            string tgtStr = targetCount < 0 ? "N/A" : targetCount.ToString("N0");
            string line = $"  │ {table,-28} │ {srcStr,10} │ {tgtStr,10} │ ";
            LogWrite(line);
            Console.ForegroundColor = color;
            Console.Write(status.PadRight(8));
            Console.ResetColor();
            Console.WriteLine("│");
            _logWriter?.WriteLine($"{status.PadRight(8)}│");
        }

        Log("  └──────────────────────────────┴────────────┴────────────┴──────────┘");
    }

    #endregion

    #region Import Phases

    private static async Task ImportPhase(string phaseId, string phaseName, string[] tables)
    {
        Log("═══════════════════════════════════════════════════════════════");
        LogColored($"    PHASE {phaseId}: {phaseName} {(_dryRun ? "(DRY RUN)" : "")}", _dryRun ? ConsoleColor.Cyan : ConsoleColor.Yellow);
        Log("═══════════════════════════════════════════════════════════════");
        Log("");

        foreach (string table in tables) {
            await MigrateTable(table);
        }

        Log("");
        if (_dryRun)
            LogColored($"  ✓ Phase {phaseId} preview complete! No data was written.", ConsoleColor.Cyan);
        else
            LogColored($"  ✓ Phase {phaseId} complete! {phaseName} imported.", ConsoleColor.Green);
    }

    private static async Task RunAllPhases()
    {
        Log("═══════════════════════════════════════════════════════════════");
        LogColored($"    FULL MIGRATION (All Phases) {(_dryRun ? "(DRY RUN)" : "")}", _dryRun ? ConsoleColor.Cyan : ConsoleColor.Yellow);
        Log("═══════════════════════════════════════════════════════════════");
        Log("");

        await ImportPhase("A", "FOUNDATION", _config.PhaseA);
        await ImportPhase("B", "USERS & CONFIG", _config.PhaseB);
        await ImportPhase("C", "APPLICATION DATA", _config.PhaseC);

        Log("");
        if (_dryRun)
            LogColored("  ✓ Full migration preview complete! No data was written.", ConsoleColor.Cyan);
        else
            LogColored("  ✓ Full migration complete!", ConsoleColor.Green);
    }

    #endregion

    #region Core: Generic Table Copier

    /// <summary>
    /// Generic table migration — copies all rows from source to target for any table.
    /// Discovers columns dynamically from INFORMATION_SCHEMA, detects PK and identity
    /// columns, skips existing rows, and uses SqlBulkCopy for high-speed transfers.
    ///
    /// TRANSFORM HOOK:
    ///   For cross-schema migrations where column names or types differ between source
    ///   and target, modify the row-reading loop below. Example:
    ///
    ///     // Instead of copying column values directly:
    ///     row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
    ///
    ///     // Map a renamed column:
    ///     if (sourceColumns[i] == "OldColumnName")
    ///         row[targetColumnIndex] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
    ///
    ///     // Transform a value during copy:
    ///     if (sourceColumns[i] == "Status")
    ///         row[i] = MapStatusValue(reader.GetInt32(i));  // int → string, etc.
    ///
    /// ID TRANSFORM HOOK:
    ///   For int → GUID primary key changes, add a MigrationTracking table and
    ///   replace the PK value with a new GUID while recording the mapping:
    ///
    ///     var newGuid = Guid.NewGuid();
    ///     row[pkOrdinal] = newGuid;
    ///     trackingEntries.Add(new { Table = tableName, OldId = oldPk, NewGuid = newGuid });
    ///
    ///   Then resolve FK references using the tracking table in later phases.
    /// </summary>
    private static async Task MigrateTable(string tableName)
    {
        int sourceCount = await GetTableCountAsync(_config.SourceDb, tableName);
        int targetCount = await GetTableCountAsync(_config.TargetDb, tableName);

        Log($"  [{tableName}] Source: {sourceCount:N0}, Target: {targetCount:N0}");

        if (sourceCount < 0) {
            LogColored($"    Table not found in source database.", ConsoleColor.DarkGray);
            return;
        }
        if (sourceCount == 0) {
            Log($"    Nothing to import.");
            return;
        }
        if (_dryRun) {
            int wouldMigrate = targetCount < 0 ? sourceCount : Math.Max(0, sourceCount - targetCount);
            int wouldSkip = targetCount < 0 ? 0 : Math.Min(sourceCount, targetCount);
            PrintMigrationResult(wouldMigrate, wouldSkip);
            return;
        }

        try {
            await using SqlConnection sourceConn = new(_config.SourceDb);
            await using SqlConnection targetConn = new(_config.TargetDb);
            await sourceConn.OpenAsync();
            await targetConn.OpenAsync();

            // Discover columns from source schema
            List<string> columns = new();
            await using (SqlCommand cmd = new(
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t ORDER BY ORDINAL_POSITION", sourceConn)) {
                cmd.Parameters.AddWithValue("@t", tableName);
                await using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
            }

            if (columns.Count == 0) {
                LogColored($"    No columns found in source.", ConsoleColor.Red);
                return;
            }

            // Detect PK column
            string pkColumn = await GetPrimaryKeyColumn(sourceConn, tableName) ?? columns[0];

            // See if the target table has an identity column (e.g., Settings.SettingId)
            bool hasIdentity = await HasIdentityColumn(targetConn, tableName);
            if (hasIdentity)
                Log($"    Identity column detected — will use KEEP_IDENTITY");

            // Load existing PKs from target to skip duplicates
            Log($"    Loading existing target PKs...");
            HashSet<object> existingPks = new();
            await using (SqlCommand cmd = new($"SELECT [{pkColumn}] FROM [{tableName}]", targetConn)) {
                cmd.CommandTimeout = _config.BulkTimeoutSeconds;
                await using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) existingPks.Add(reader.GetValue(0));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            int migrated = 0, skipped = 0, batchNum = 0;

            // Build SELECT with all columns
            string selectCols = string.Join(", ", columns.Select(c => $"[{c}]"));
            string query = $"SELECT {selectCols} FROM [{tableName}] ORDER BY [{pkColumn}]";

            // Create DataTable for bulk insert
            System.Data.DataTable dataTable = new();

            await using (SqlCommand cmd = new(query, sourceConn)) {
                cmd.CommandTimeout = _config.BulkTimeoutSeconds;
                await using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                // Build DataTable columns from reader schema
                for (int i = 0; i < reader.FieldCount; i++) {
                    dataTable.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
                }

                // Find PK ordinal for fast lookup
                int pkOrdinal = 0;
                for (int i = 0; i < reader.FieldCount; i++) {
                    if (reader.GetName(i).Equals(pkColumn, StringComparison.OrdinalIgnoreCase)) {
                        pkOrdinal = i;
                        break;
                    }
                }

                while (await reader.ReadAsync()) {
                    object pkValue = reader.GetValue(pkOrdinal);

                    // Skip if already exists in target
                    if (existingPks.Contains(pkValue)) { skipped++; continue; }

                    System.Data.DataRow row = dataTable.NewRow();
                    for (int i = 0; i < reader.FieldCount; i++) {
                        // ─────────────────────────────────────────────────────────
                        // TRANSFORM HOOK: Modify this line for cross-schema mapping
                        // ─────────────────────────────────────────────────────────
                        row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                    }
                    dataTable.Rows.Add(row);
                    migrated++;

                    // Flush batch when full
                    if (dataTable.Rows.Count >= _config.BulkBatchSize) {
                        batchNum++;
                        await BulkInsert(targetConn, tableName, dataTable, hasIdentity);

                        double elapsed = stopwatch.Elapsed.TotalSeconds;
                        double rate = migrated / Math.Max(elapsed, 0.01);
                        int remaining = (int)((sourceCount - migrated - skipped) / Math.Max(rate, 1));
                        Console.Write($"\r    Batch {batchNum}: {migrated + skipped:N0}/{sourceCount:N0} ({rate:N0}/sec, ~{remaining / 60}m {remaining % 60}s remaining)    ");

                        dataTable.Clear();
                    }
                }
            }

            // Flush any remaining rows
            if (dataTable.Rows.Count > 0) {
                batchNum++;
                await BulkInsert(targetConn, tableName, dataTable, hasIdentity);
            }

            if (batchNum > 0) {
                Log(""); // New line after progress
                Log($"    Completed in {stopwatch.Elapsed.TotalSeconds:F1}s ({migrated / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.01):N0}/sec)");
            }

            PrintMigrationResult(migrated, skipped);
        } catch (Exception ex) {
            LogError($"Migration [{tableName}]", ex.Message, ex.InnerException?.Message);
        }
    }

    #endregion

    #region Truncate

    /// <summary>
    /// Truncates ALL tables in the target database by disabling FK constraints,
    /// deleting all rows in reverse FK order, then re-enabling constraints.
    /// </summary>
    private static async Task TruncateAllTables()
    {
        Log("═══════════════════════════════════════════════════════════════");
        LogColored("    ⚠️  TRUNCATE ALL TARGET TABLES", ConsoleColor.Red);
        Log("═══════════════════════════════════════════════════════════════");
        Log("");

        if (_dryRun) {
            LogColored("  [DRY RUN] Would truncate all tables in target database.", ConsoleColor.Cyan);
            Log("  Tables that would be truncated:");
            foreach (string table in AllTablesOrdered.Reverse())
                Log($"    - {table}");
            return;
        }

        Log("  ⚠️  This will DELETE ALL DATA from the target database!");
        Log($"  Target: {MaskConnectionString(_config.TargetDb)}");
        Log("");

        if (!ConfirmAction("truncation of ALL target tables")) return;

        Log("");

        try {
            await using SqlConnection conn = new(_config.TargetDb);
            await conn.OpenAsync();

            // Disable all FK constraints
            Log("  Disabling FK constraints...");
            await using (SqlCommand cmd = new(
                "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'", conn)) {
                cmd.CommandTimeout = 120;
                await cmd.ExecuteNonQueryAsync();
            }

            // Delete from each table in reverse FK order
            foreach (string table in AllTablesOrdered.Reverse()) {
                int count = await GetTableCountAsync(_config.TargetDb, table);
                if (count < 0) {
                    Log($"    {table}: not found, skipping");
                    continue;
                }

                LogWrite($"    Deleting {table} ({count:N0} rows)... ");
                try {
                    await using SqlCommand delCmd = new($"DELETE FROM [{table}]", conn);
                    delCmd.CommandTimeout = 600;
                    await delCmd.ExecuteNonQueryAsync();

                    // Reset identity seed if applicable
                    if (await HasIdentityColumn(conn, table)) {
                        await using SqlCommand identCmd = new(
                            $"DBCC CHECKIDENT('{table}', RESEED, 0)", conn);
                        await identCmd.ExecuteNonQueryAsync();
                    }

                    LogColored("✓", ConsoleColor.Green);
                } catch (Exception ex) {
                    LogColored($"FAILED: {ex.Message}", ConsoleColor.Red);
                }
            }

            // Re-enable FK constraints
            Log("  Re-enabling FK constraints...");
            await using (SqlCommand cmd = new(
                "EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'", conn)) {
                cmd.CommandTimeout = 120;
                await cmd.ExecuteNonQueryAsync();
            }

            Log("");
            LogColored("  ✓ All tables truncated.", ConsoleColor.Green);
        } catch (Exception ex) {
            LogError("Truncation Error", ex.Message, ex.InnerException?.Message);
        }
    }

    #endregion

    #region Preview Sample Data

    private static async Task PreviewSampleData()
    {
        Log("═══════════════════════════════════════════════════════════════");
        Log("                    SAMPLE DATA PREVIEW                        ");
        Log("═══════════════════════════════════════════════════════════════");
        Log("");

        foreach (string table in AllTablesOrdered) {
            int count = await GetTableCountAsync(_config.SourceDb, table);
            if (count <= 0) continue;

            Log($"  {table} ({count:N0} total rows, showing first 3):");

            try {
                await using SqlConnection conn = new(_config.SourceDb);
                await conn.OpenAsync();

                // Get column names
                List<string> columns = new();
                await using (SqlCommand cmd = new(
                    "SELECT TOP 3 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t ORDER BY ORDINAL_POSITION", conn)) {
                    cmd.Parameters.AddWithValue("@t", table);
                    await using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
                }

                // First 3 columns only for readability
                string cols = string.Join(", ", columns.Take(3).Select(c => $"[{c}]"));
                await using (SqlCommand cmd = new($"SELECT TOP 3 {cols} FROM [{table}]", conn)) {
                    cmd.CommandTimeout = 30;
                    await using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) {
                        List<string> vals = new();
                        for (int i = 0; i < reader.FieldCount; i++) {
                            string val = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString() ?? "";
                            if (val.Length > 40) val = val[..37] + "...";
                            vals.Add(val);
                        }
                        Log($"    [{string.Join("] [", vals)}]");
                    }
                }

                Log("");
            } catch (Exception ex) {
                Log($"    Error: {ex.Message}");
                Log("");
            }
        }
    }

    #endregion

    #region Data Integrity Verification

    /// <summary>
    /// Exports rows from both source and target databases to normalized CSV files,
    /// computes SHA256 hashes, and compares them to verify data integrity.
    /// Files are saved in runs/data/latest/{source|target}/ for inspection.
    /// </summary>
    private static async Task RunDataIntegrityVerification()
    {
        Log("═══════════════════════════════════════════════════════════════");
        Log("                DATA INTEGRITY VERIFICATION                    ");
        Log("═══════════════════════════════════════════════════════════════");
        Log("");
        Log("  Exports both databases to normalized CSV files and compares");
        Log("  SHA256 hashes to verify data integrity after migration.");
        Log("");

        string dataDir = Path.Combine(_runsDirectory, "data", "latest");
        string sourceDir = Path.Combine(dataDir, "source");
        string targetDir = Path.Combine(dataDir, "target");

        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(targetDir);

        int matchCount = 0, mismatchCount = 0, skipCount = 0;

        Log("  ┌──────────────────────────────┬──────────┬──────────┬──────────┐");
        Log("  │ Table                        │ Source   │ Target   │ Status   │");
        Log("  ├──────────────────────────────┼──────────┼──────────┼──────────┤");

        foreach (string table in AllTablesOrdered) {
            int sourceCount = await GetTableCountAsync(_config.SourceDb, table);
            int targetCount = await GetTableCountAsync(_config.TargetDb, table);

            if (sourceCount <= 0 && targetCount <= 0) {
                skipCount++;
                continue;
            }

            string srcFile = Path.Combine(sourceDir, $"{table}.csv");
            string tgtFile = Path.Combine(targetDir, $"{table}.csv");

            string srcHash = await ExportTableToCsvAndHash(_config.SourceDb, table, srcFile);
            string tgtHash = await ExportTableToCsvAndHash(_config.TargetDb, table, tgtFile);

            bool match = srcHash == tgtHash && srcHash != "ERROR" && srcHash != "EMPTY";
            string status = (srcHash, tgtHash) switch {
                ("ERROR", _) or (_, "ERROR") => "ERROR",
                ("EMPTY", "EMPTY") => "EMPTY",
                _ when match => "MATCH",
                _ => "DIFFER"
            };

            ConsoleColor color = status switch {
                "MATCH" => ConsoleColor.Green,
                "DIFFER" => ConsoleColor.Red,
                "EMPTY" => ConsoleColor.DarkGray,
                _ => ConsoleColor.Red
            };

            if (status == "MATCH") matchCount++;
            else if (status == "DIFFER") mismatchCount++;

            string line = $"  │ {table,-28} │ {sourceCount.ToString("N0"),8} │ {targetCount.ToString("N0"),8} │ ";
            LogWrite(line);
            Console.ForegroundColor = color;
            Console.Write(status.PadRight(8));
            Console.ResetColor();
            Console.WriteLine("│");
            _logWriter?.WriteLine($"{status.PadRight(8)}│");
        }

        Log("  └──────────────────────────────┴──────────┴──────────┴──────────┘");
        Log("");
        Log($"  Results: {matchCount} MATCH, {mismatchCount} DIFFER, {skipCount} SKIPPED");
        Log($"  CSV files exported to: {dataDir}");

        if (mismatchCount > 0)
            LogColored("  ⚠️  Some tables have data differences! Check the CSV files.", ConsoleColor.Yellow);
        else if (matchCount > 0)
            LogColored("  ✓ All populated tables have matching data.", ConsoleColor.Green);
    }

    private static async Task<string> ExportTableToCsvAndHash(string connectionString, string tableName, string outputPath)
    {
        try {
            await using SqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            // Get PK column for consistent ordering
            string? pkColumn = await GetPrimaryKeyColumn(conn, tableName);

            string orderBy = pkColumn != null ? $"ORDER BY [{pkColumn}]" : "";
            await using SqlCommand cmd = new($"SELECT * FROM [{tableName}] {orderBy}", conn);
            cmd.CommandTimeout = _config.BulkTimeoutSeconds;

            await using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            using SHA256 sha256 = SHA256.Create();
            await using FileStream fs = File.Create(outputPath);
            await using StreamWriter writer = new(fs, Encoding.UTF8);

            // Header row
            List<string> headers = new();
            for (int i = 0; i < reader.FieldCount; i++) headers.Add(reader.GetName(i));
            string headerLine = string.Join(",", headers);
            writer.WriteLine(headerLine);

            int rowCount = 0;
            while (await reader.ReadAsync()) {
                List<string> values = new();
                for (int i = 0; i < reader.FieldCount; i++) {
                    string val = reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString() ?? "";
                    // Escape commas and quotes for CSV
                    if (val.Contains(',') || val.Contains('"') || val.Contains('\n'))
                        val = $"\"{val.Replace("\"", "\"\"")}\"";
                    values.Add(val);
                }
                writer.WriteLine(string.Join(",", values));
                rowCount++;
            }

            writer.Flush();
            if (rowCount == 0) return "EMPTY";

            // Compute hash of the file
            fs.Position = 0;
            byte[] hash = await sha256.ComputeHashAsync(fs);
            return Convert.ToHexStringLower(hash);
        } catch {
            return "ERROR";
        }
    }

    #endregion

    #region Column Profiling

    /// <summary>
    /// Analyzes the source database schema — reports column data types,
    /// null counts, min/max lengths (strings), min/max values (numerics).
    /// Output saved to runs/column-profile.txt for review.
    /// </summary>
    private static async Task RunColumnProfiling()
    {
        Log("═══════════════════════════════════════════════════════════════");
        Log("                    COLUMN PROFILING                           ");
        Log("═══════════════════════════════════════════════════════════════");
        Log("");
        Log("  Analyzing source database schema and data distribution...");
        Log("");

        string reportPath = Path.Combine(_runsDirectory, "column-profile.txt");
        await using StreamWriter writer = new(reportPath, false, Encoding.UTF8);

        writer.WriteLine($"Column Profile Report — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"Source: {MaskConnectionString(_config.SourceDb)}");
        writer.WriteLine(new string('=', 120));
        writer.WriteLine();

        await using SqlConnection conn = new(_config.SourceDb);
        await conn.OpenAsync();

        foreach (string table in AllTablesOrdered) {
            int count = await GetTableCountAsync(_config.SourceDb, table);
            if (count < 0) continue;

            writer.WriteLine($"TABLE: {table} ({count:N0} rows)");
            writer.WriteLine($"{"Column",-30} {"Type",-15} {"SchemaMax",10} {"Nulls",8} {"MinLen",8} {"MaxLen",8} {"MinVal",15} {"MaxVal",15}");
            writer.WriteLine(new string('-', 120));

            // Get columns with their types and max lengths
            List<(string colName, string dataType, int? schemaMaxLen)> columns = new();
            await using (SqlCommand cmd = new(
                "SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH " +
                "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t ORDER BY ORDINAL_POSITION", conn)) {
                cmd.Parameters.AddWithValue("@t", table);
                await using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) {
                    columns.Add((
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetInt32(2)
                    ));
                }
            }

            if (count == 0) {
                foreach (var (colName, dataType, schemaMaxLen) in columns) {
                    string schemaStr = schemaMaxLen.HasValue ? schemaMaxLen.Value == -1 ? "MAX" : schemaMaxLen.Value.ToString() : "";
                    writer.WriteLine($"{colName,-30} {dataType,-15} {schemaStr,10}");
                }
                writer.WriteLine();
                continue;
            }

            foreach (var (colName, dataType, schemaMaxLen) in columns) {
                bool isString = dataType is "nvarchar" or "varchar" or "nchar" or "char" or "text" or "ntext";
                bool isNumeric = dataType is "int" or "bigint" or "smallint" or "tinyint" or "decimal" or "numeric" or "float" or "real" or "money";

                string nullCount = "", minLen = "", maxLen = "", minVal = "", maxVal = "";

                try {
                    if (isString) {
                        string lenFn = dataType is "text" or "ntext" ? "DATALENGTH" : "LEN";
                        string divider = dataType is "ntext" or "nvarchar" or "nchar" ? " / 2" : "";
                        await using SqlCommand cmd = new(
                            $"SELECT " +
                            $"SUM(CASE WHEN [{colName}] IS NULL THEN 1 ELSE 0 END), " +
                            $"MIN(CASE WHEN [{colName}] IS NULL THEN 0 ELSE CAST({lenFn}([{colName}]){divider} AS INT) END), " +
                            $"MAX(CASE WHEN [{colName}] IS NULL THEN 0 ELSE CAST({lenFn}([{colName}]){divider} AS INT) END) " +
                            $"FROM [{table}]", conn);
                        await using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync()) {
                            nullCount = reader.IsDBNull(0) ? "0" : reader.GetInt32(0).ToString("N0");
                            minLen = reader.IsDBNull(1) ? "" : reader.GetInt32(1).ToString();
                            maxLen = reader.IsDBNull(2) ? "" : reader.GetInt32(2).ToString();
                        }
                    } else if (isNumeric) {
                        await using SqlCommand cmd = new(
                            $"SELECT " +
                            $"SUM(CASE WHEN [{colName}] IS NULL THEN 1 ELSE 0 END), " +
                            $"MIN([{colName}]), MAX([{colName}]) " +
                            $"FROM [{table}]", conn);
                        await using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync()) {
                            nullCount = reader.IsDBNull(0) ? "0" : reader.GetInt32(0).ToString("N0");
                            minVal = reader.IsDBNull(1) ? "" : reader.GetValue(1).ToString() ?? "";
                            maxVal = reader.IsDBNull(2) ? "" : reader.GetValue(2).ToString() ?? "";
                        }
                    } else {
                        await using SqlCommand cmd = new(
                            $"SELECT SUM(CASE WHEN [{colName}] IS NULL THEN 1 ELSE 0 END) FROM [{table}]", conn);
                        object? result = await cmd.ExecuteScalarAsync();
                        nullCount = result is DBNull or null ? "0" : ((int)result).ToString("N0");
                    }
                } catch {
                    nullCount = "ERR";
                }

                string schemaStr = schemaMaxLen.HasValue ? schemaMaxLen.Value == -1 ? "MAX" : schemaMaxLen.Value.ToString() : "";
                writer.WriteLine($"{colName,-30} {dataType,-15} {schemaStr,10} {nullCount,8} {minLen,8} {maxLen,8} {minVal,15} {maxVal,15}");
            }

            writer.WriteLine();

            // Log progress to console
            LogColored($"  ✓ {table} ({columns.Count} columns)", ConsoleColor.Green);
        }

        writer.Flush();
        Log("");
        Log($"  Profile saved to: {reportPath}");
    }

    #endregion

    #region EF Schema Management

    /// <summary>
    /// Full workflow: Delete old migrations → generate fresh InitialMigration → apply to target DB.
    /// This creates the target database from scratch using the EFModels schema.
    ///
    /// WHY FRESH MIGRATIONS EVERY TIME:
    ///   EF migration files are snapshots of the model at a point in time. If the model
    ///   has changed since the last migration was generated, the migration files are stale.
    ///   By deleting and regenerating, we guarantee the migration matches the CURRENT model.
    ///   This is safe because we're creating a brand new database — there's no existing
    ///   data to preserve, so we don't need incremental migrations.
    ///
    /// WHAT THIS RUNS (equivalent to Package Manager Console commands):
    ///   1. Delete EFModels/Migrations/* folder contents
    ///   2. dotnet ef migrations add InitialMigration --project EFModels --context EFDataModel
    ///      --connection "TargetDb connection string"
    ///   3. dotnet ef database update --project EFModels --context EFDataModel
    ///      --connection "TargetDb connection string"
    /// </summary>
    private static async Task CreateTargetDatabase()
    {
        Log("═══════════════════════════════════════════════════════════════");
        LogColored("    EF SCHEMA: CREATE TARGET DATABASE", ConsoleColor.Yellow);
        Log("═══════════════════════════════════════════════════════════════");
        Log("");
        Log("  This will:");
        Log("    1. Delete existing EF migration files (clean slate)");
        Log("    2. Generate a fresh InitialMigration from the current model");
        Log("    3. Apply it to the target database (creates DB if missing)");
        Log("");
        Log($"  Target: {MaskConnectionString(_config.TargetDb)}");
        Log($"  EF Project: {_config.EfModelsProject}");
        Log("");

        if (_dryRun) {
            LogColored("  [DRY RUN] Would create target database. Toggle dry run to execute.", ConsoleColor.Cyan);
            return;
        }

        if (!ConfirmAction("creating the target database from EF models")) return;

        Log("");

        // Step 1: Generate fresh migration
        bool migrationOk = await GenerateFreshMigration();
        if (!migrationOk) {
            LogError("Create DB", "Migration generation failed. Aborting database update.");
            return;
        }

        // Step 2: Apply to target DB
        await ApplyMigrationToTarget();
    }

    /// <summary>
    /// Deletes existing migration files and generates a fresh InitialMigration.
    /// Equivalent to: Remove-Migration -Force + Add-Migration InitialMigration
    ///
    /// HOW IT WORKS:
    ///   The EF tools need to instantiate the DbContext at design time to read the model.
    ///   Since the EFDataModel doesn't have OnConfiguring (it's injected at runtime via DI),
    ///   we write a TEMPORARY IDesignTimeDbContextFactory file into the EFModels project.
    ///   This factory provides the target connection string so the EF tools can work.
    ///   The temp file is deleted after the commands finish (success or failure).
    /// </summary>
    private static async Task<bool> GenerateFreshMigration()
    {
        Log("═══════════════════════════════════════════════════════════════");
        LogColored("    EF SCHEMA: GENERATE FRESH MIGRATION", ConsoleColor.Yellow);
        Log("═══════════════════════════════════════════════════════════════");
        Log("");

        string solutionRoot = ResolveSolutionRoot();
        if (string.IsNullOrEmpty(solutionRoot)) {
            LogError("EF Migration", "Cannot find solution root. Set 'SolutionRoot' in appsettings.json.");
            return false;
        }

        string efProjectPath = Path.Combine(solutionRoot, _config.EfModelsProject);
        if (!File.Exists(efProjectPath)) {
            LogError("EF Migration", $"EF Models project not found: {efProjectPath}");
            Log("  Set 'EfModelsProject' in appsettings.json to the correct relative path.");
            return false;
        }

        string efProjectDir = Path.GetDirectoryName(efProjectPath)!;
        string migrationsDir = Path.Combine(efProjectDir, "Migrations");

        // Step 1: Delete existing migration files
        if (Directory.Exists(migrationsDir)) {
            int fileCount = Directory.GetFiles(migrationsDir, "*.*", SearchOption.AllDirectories).Length;
            LogWrite($"  Deleting {fileCount} existing migration files in Migrations/... ");
            if (!_dryRun) {
                Directory.Delete(migrationsDir, recursive: true);
                LogColored("✓", ConsoleColor.Green);
            } else {
                LogColored("[DRY RUN] would delete", ConsoleColor.Cyan);
            }
        } else {
            Log("  No existing Migrations/ folder found (clean start).");
        }

        if (_dryRun) {
            LogColored("  [DRY RUN] Would generate fresh InitialMigration.", ConsoleColor.Cyan);
            return true;
        }

        // Step 2: Write temporary design-time factory so the EF tools can instantiate the context.
        //   migrations add does NOT accept --connection, so the tools need another way to
        //   create the DbContext. An IDesignTimeDbContextFactory is the standard pattern.
        string factoryFile = Path.Combine(efProjectDir, "_DesignTimeFactory.cs");
        WriteDesignTimeFactory(factoryFile, _config.TargetDb);
        Log($"  Created temp design-time factory: _DesignTimeFactory.cs");

        try {
            // Step 3: Generate new migration
            Log("");
            Log("  Generating InitialMigration...");
            Log($"    Project: {efProjectPath}");
            Log($"    Context: EFDataModel");
            Log($"    Target:  {MaskConnectionString(_config.TargetDb)}");
            Log("");

            // --startup-project = EFModels itself so the EF tools only scan that assembly
            // and find the factory we just wrote (avoids "More than one DbContext" errors
            // when the solution has duplicate context names in ReferenceProjects).
            string addMigrationArgs = $"ef migrations add InitialMigration " +
                $"--project \"{efProjectPath}\" " +
                $"--startup-project \"{efProjectPath}\"";

            var (success, output) = await RunDotnetCommand(addMigrationArgs, solutionRoot);

            if (success) {
                LogColored("  ✓ Migration generated successfully.", ConsoleColor.Green);

                // Show what was created
                if (Directory.Exists(migrationsDir)) {
                    string[] newFiles = Directory.GetFiles(migrationsDir, "*.cs");
                    foreach (string file in newFiles) {
                        Log($"    Created: {Path.GetFileName(file)}");
                    }
                }
            } else {
                LogError("EF Migration", "Failed to generate migration.");
                if (!string.IsNullOrWhiteSpace(output))
                    Log($"  Output: {output}");
            }

            Log("");
            return success;
        } finally {
            // Always clean up the temporary factory file
            CleanupDesignTimeFactory(factoryFile);
        }
    }

    /// <summary>
    /// Applies existing EF migrations to the target database.
    /// Creates the database if it doesn't exist.
    /// Equivalent to: Update-Database -Context EFDataModel -ConnectionString "..."
    /// </summary>
    private static async Task ApplyMigrationToTarget()
    {
        Log("═══════════════════════════════════════════════════════════════");
        LogColored("    EF SCHEMA: APPLY MIGRATION TO TARGET", ConsoleColor.Yellow);
        Log("═══════════════════════════════════════════════════════════════");
        Log("");

        string solutionRoot = ResolveSolutionRoot();
        if (string.IsNullOrEmpty(solutionRoot)) {
            LogError("EF Update", "Cannot find solution root. Set 'SolutionRoot' in appsettings.json.");
            return;
        }

        string efProjectPath = Path.Combine(solutionRoot, _config.EfModelsProject);
        if (!File.Exists(efProjectPath)) {
            LogError("EF Update", $"EF Models project not found: {efProjectPath}");
            return;
        }

        if (_dryRun) {
            LogColored("  [DRY RUN] Would apply migrations to target database.", ConsoleColor.Cyan);
            Log($"    Target: {MaskConnectionString(_config.TargetDb)}");
            return;
        }

        Log($"  Target: {MaskConnectionString(_config.TargetDb)}");
        Log($"  Project: {efProjectPath}");
        Log("");

        // Write temporary design-time factory (same reason as GenerateFreshMigration)
        string efProjectDir = Path.GetDirectoryName(efProjectPath)!;
        string factoryFile = Path.Combine(efProjectDir, "_DesignTimeFactory.cs");
        WriteDesignTimeFactory(factoryFile, _config.TargetDb);
        Log($"  Created temp design-time factory: _DesignTimeFactory.cs");
        Log("  Applying migrations (creates DB if missing)...");

        try {
            string updateArgs = $"ef database update " +
                $"--project \"{efProjectPath}\" " +
                $"--startup-project \"{efProjectPath}\" " +
                $"--connection \"{_config.TargetDb}\"";

            var (success, output) = await RunDotnetCommand(updateArgs, solutionRoot);

            Log("");
            if (success) {
                LogColored("  ✓ Database updated successfully.", ConsoleColor.Green);

                // Verify connectivity to the new database
                LogWrite("  Verifying target connectivity... ");
                await TestConnectivity("Target", _config.TargetDb);
            } else {
                LogError("EF Update", "Failed to apply migrations.");
                if (!string.IsNullOrWhiteSpace(output))
                    Log($"  Output: {output}");
            }
        } finally {
            CleanupDesignTimeFactory(factoryFile);
        }
    }

    /// <summary>
    /// Writes a temporary IDesignTimeDbContextFactory into the EFModels project.
    /// The EF CLI tools discover this factory automatically and use it to create
    /// the DbContext at design time (for migrations add / database update).
    /// The file is deleted after the EF commands complete.
    /// </summary>
    private static void WriteDesignTimeFactory(string filePath, string connectionString)
    {
        // Escape backslashes and quotes for the C# string literal
        string escapedCs = connectionString.Replace("\\", "\\\\").Replace("\"", "\\\"");

        string factoryCode =
            "// AUTO-GENERATED by FreeExamples.DatabaseMigration tool.\n" +
            "// This file is temporary and will be deleted after EF commands run.\n" +
            "// DO NOT CHECK THIS FILE INTO SOURCE CONTROL.\n" +
            "using Microsoft.EntityFrameworkCore;\n" +
            "using Microsoft.EntityFrameworkCore.Design;\n" +
            "\n" +
            "namespace FreeExamples.EFModels.EFModels;\n" +
            "\n" +
            "public class DesignTimeFactory : IDesignTimeDbContextFactory<EFDataModel>\n" +
            "{\n" +
            "    public EFDataModel CreateDbContext(string[] args)\n" +
            "    {\n" +
            "        var optionsBuilder = new DbContextOptionsBuilder<EFDataModel>();\n" +
            $"        optionsBuilder.UseSqlServer(\"{escapedCs}\");\n" +
            "        return new EFDataModel(optionsBuilder.Options);\n" +
            "    }\n" +
            "}\n";

        File.WriteAllText(filePath, factoryCode);
    }

    /// <summary>
    /// Deletes the temporary design-time factory file.
    /// </summary>
    private static void CleanupDesignTimeFactory(string filePath)
    {
        try {
            if (File.Exists(filePath)) {
                File.Delete(filePath);
                Log("  Cleaned up temp design-time factory.");
            }
        } catch (Exception ex) {
            LogColored($"  Warning: Could not delete temp factory file: {ex.Message}", ConsoleColor.Yellow);
            Log($"  You can safely delete it manually: {filePath}");
        }
    }

    /// <summary>
    /// Runs a dotnet CLI command and streams output to the console + log.
    /// </summary>
    private static async Task<(bool Success, string Output)> RunDotnetCommand(string arguments, string workingDirectory)
    {
        try {
            ProcessStartInfo psi = new() {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            Log($"  > dotnet {arguments}", logOnly: true);

            using Process? process = Process.Start(psi);
            if (process == null) {
                LogError("Process", "Failed to start dotnet process.");
                return (false, "Failed to start process");
            }

            // Read output line by line so the user sees progress
            StringBuilder outputBuilder = new();
            StringBuilder errorBuilder = new();

            Task readOutput = Task.Run(async () => {
                while (await process.StandardOutput.ReadLineAsync() is string line) {
                    outputBuilder.AppendLine(line);
                    // Show EF tool output with indent
                    if (!string.IsNullOrWhiteSpace(line)) {
                        Log($"    {line.TrimEnd()}");
                    }
                }
            });

            Task readError = Task.Run(async () => {
                while (await process.StandardError.ReadLineAsync() is string line) {
                    errorBuilder.AppendLine(line);
                    if (!string.IsNullOrWhiteSpace(line)) {
                        LogColored($"    {line.TrimEnd()}", ConsoleColor.Red);
                    }
                }
            });

            await process.WaitForExitAsync();
            await Task.WhenAll(readOutput, readError);

            string combined = (outputBuilder.ToString() + errorBuilder.ToString()).Trim();
            return (process.ExitCode == 0, combined);
        } catch (Exception ex) {
            LogError("Process Error", ex.Message);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Walks up from the running binary to find the solution root (directory containing .sln file).
    /// Returns the configured SolutionRoot if set, otherwise auto-detects.
    /// </summary>
    private static string ResolveSolutionRoot()
    {
        // Use configured value if set
        if (!string.IsNullOrWhiteSpace(_config.SolutionRoot)) {
            string expanded = Environment.ExpandEnvironmentVariables(_config.SolutionRoot);
            if (Directory.Exists(expanded)) return expanded;
        }

        // Auto-detect: walk up from the binary location looking for a .sln file
        string currentDir = AppContext.BaseDirectory;
        for (int i = 0; i < 15; i++) {
            if (Directory.GetFiles(currentDir, "*.sln").Length > 0)
                return currentDir;

            DirectoryInfo? parent = Directory.GetParent(currentDir);
            if (parent == null) break;
            currentDir = parent.FullName;
        }

        // Also try current working directory and walk up
        currentDir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10; i++) {
            if (Directory.GetFiles(currentDir, "*.sln").Length > 0)
                return currentDir;

            DirectoryInfo? parent = Directory.GetParent(currentDir);
            if (parent == null) break;
            currentDir = parent.FullName;
        }

        return "";
    }

    #endregion

    #region Helpers

    private static async Task<int> GetTableCountAsync(string connectionString, string tableName)
    {
        try {
            await using SqlConnection conn = new(connectionString);
            await conn.OpenAsync();
            await using SqlCommand cmd = new($"SELECT COUNT(*) FROM [{tableName}]", conn);
            cmd.CommandTimeout = 30;
            object? result = await cmd.ExecuteScalarAsync();
            return result is int i ? i : Convert.ToInt32(result);
        } catch {
            return -1;
        }
    }

    private static async Task<string?> GetPrimaryKeyColumn(SqlConnection conn, string tableName)
    {
        await using SqlCommand cmd = new(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE " +
            "WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + QUOTENAME(CONSTRAINT_NAME)), 'IsPrimaryKey') = 1 " +
            "AND TABLE_NAME = @t " +
            "ORDER BY ORDINAL_POSITION", conn);
        cmd.Parameters.AddWithValue("@t", tableName);
        object? result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    private static async Task<bool> HasIdentityColumn(SqlConnection conn, string tableName)
    {
        await using SqlCommand cmd = new(
            "SELECT COUNT(*) FROM sys.identity_columns ic " +
            "JOIN sys.tables t ON ic.object_id = t.object_id " +
            "WHERE t.name = @t", conn);
        cmd.Parameters.AddWithValue("@t", tableName);
        object? result = await cmd.ExecuteScalarAsync();
        return result is int i && i > 0;
    }

    private static async Task BulkInsert(SqlConnection connection, string tableName, System.Data.DataTable dataTable, bool keepIdentity = false)
    {
        SqlBulkCopyOptions options = keepIdentity
            ? SqlBulkCopyOptions.KeepIdentity
            : SqlBulkCopyOptions.Default;

        using SqlBulkCopy bulkCopy = new(connection, options, null) {
            DestinationTableName = tableName,
            BulkCopyTimeout = _config.BulkTimeoutSeconds,
            BatchSize = _config.BulkBatchSize
        };

        foreach (System.Data.DataColumn col in dataTable.Columns) {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(dataTable);
    }

    #endregion
}

// ═══════════════════════════════════════════════════════════════════════════════
// Configuration model — populated from appsettings.json "Migration" section
// ═══════════════════════════════════════════════════════════════════════════════
public class MigrationConfig
{
    // Connection strings
    public string SourceDb { get; set; } = "";
    public string TargetDb { get; set; } = "";

    // Bulk insert tuning
    public int BulkBatchSize { get; set; } = 5000;
    public int BulkTimeoutSeconds { get; set; } = 300;

    // Startup behavior
    public bool DryRunOnStart { get; set; } = true;

    // CLI automation
    public string AutoRun { get; set; } = "";
    public bool AutoConfirm { get; set; } = false;

    // Phase definitions (table names in FK dependency order)
    // NOTE: Defaults are empty — values come from appsettings.json.
    // If you set defaults here, the config binder APPENDS json values onto them (doubling).
    public string[] PhaseA { get; set; } = [];
    public string[] PhaseB { get; set; } = [];
    public string[] PhaseC { get; set; } = [];

    // EF Models project path (relative to solution root)
    // Used by the EF Schema Management commands to locate the EFModels .csproj
    public string EfModelsProject { get; set; } = "";

    // Solution root directory (absolute path)
    // Auto-detected if empty — walks up from the running binary to find the .sln file
    public string SolutionRoot { get; set; } = "";
}
