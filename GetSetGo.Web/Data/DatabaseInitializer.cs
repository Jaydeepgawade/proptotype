using GetSetGo.Web.Services;
using Microsoft.Data.SqlClient;

namespace GetSetGo.Web.Data;

public sealed class DatabaseInitializer(IConfiguration configuration, IPasswordService passwords)
{
    public async Task InitializeAsync()
    {
        var connectionString = configuration.GetConnectionString("GetSetGoDb")
            ?? throw new InvalidOperationException("GetSetGoDb connection string is missing.");
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName) || databaseName.Length > 128)
            throw new InvalidOperationException("A database name (Initial Catalog) between 1 and 128 characters is required.");

        // Azure SQL databases are provisioned separately; connect directly to the
        // configured catalog without requiring access to the logical server's master.
        var server = builder.DataSource;
        if (server.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) server = server[4..];
        var host = server.Split(',')[0].Trim().TrimEnd('.');
        var isAzureSql = host.EndsWith(".database.windows.net", StringComparison.OrdinalIgnoreCase);
        var createDatabase = configuration.GetValue<bool?>("Database:CreateIfMissing") ?? !isAzureSql;
        if (createDatabase)
        {
            builder.InitialCatalog = "master";
            await using var master = new SqlConnection(builder.ConnectionString);
            await master.OpenAsync();
            await using var create = master.CreateCommand();
            // Parameterize the lookup and quote the identifier independently.
            // This supports names such as get-set-go without allowing SQL injection.
            using var commands = new SqlCommandBuilder();
            var quotedName = commands.QuoteIdentifier(databaseName);
            create.CommandText = $"IF DB_ID(@DatabaseName) IS NULL CREATE DATABASE {quotedName};";
            create.Parameters.Add("@DatabaseName", System.Data.SqlDbType.NVarChar, 128).Value = databaseName;
            await create.ExecuteNonQueryAsync();
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync();
        }

        await SeedUserAsync(connection, "Demo Client", "client@getsetgo.local", "Client@123", "Client");
        await SeedUserAsync(connection, "Research Admin", "admin@getsetgo.local", "Admin@123", "Admin");

        await SeedMarketCandlesAsync(connection);
    }

    internal static async Task SeedMarketCandlesAsync(SqlConnection connection)
    {
        // These prices and candles are synthetic demo data, not exchange prices.
        var instruments = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["RELIANCE"] = 2950m, ["TCS"] = 4180m, ["HDFCBANK"] = 1720m,
            ["M&M"] = 3100m, ["KSB"] = 100m, ["RELIANCE X"] = 100m,
            ["INFY"] = 1500m, ["ICICIBANK"] = 1200m, ["SBIN"] = 800m,
            ["ITC"] = 450m, ["TATAMOTORS"] = 950m, ["WIPRO"] = 500m
        };
        // Use each published symbol's latest entry as its demo starting price.
        await using (var signals = connection.CreateCommand())
        {
            signals.CommandText = "SELECT Symbol,EntryPrice FROM ResearchSignals ORDER BY Id";
            await using var reader = await signals.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                instruments[reader.GetString(0).Trim().ToUpperInvariant()] = reader.GetDecimal(1);
        }

        var dates = new List<DateTime>();
        for (var date = DateTime.UtcNow.Date.AddDays(-1); dates.Count < 60; date = date.AddDays(-1))
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) dates.Add(date);
        dates.Reverse();

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        foreach (var instrument in instruments.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            // Check per symbol so an existing database also receives newly added companies.
            await using var exists = connection.CreateCommand();
            exists.Transaction = transaction;
            exists.CommandText = "SELECT COUNT_BIG(1) FROM MarketCandles WITH (UPDLOCK,HOLDLOCK) WHERE Symbol=@Symbol";
            exists.Parameters.AddWithValue("@Symbol", instrument.Key);
            if (Convert.ToInt64(await exists.ExecuteScalarAsync()) > 0) continue;

            var seed = 17;
            foreach (var character in instrument.Key) seed = unchecked(seed * 31 + character);
            var random = new Random(seed);
            var price = Math.Max(.01m, instrument.Value);
            foreach (var date in dates)
            {
                var open = Math.Max(.01m, Math.Round(price * (1m + (decimal)(random.NextDouble() - .5) * .012m), 2));
                var close = Math.Max(.01m, Math.Round(open * (1m + (decimal)(random.NextDouble() - .5) * .018m), 2));
                var high = Math.Round(Math.Max(open, close) * (1m + (decimal)random.NextDouble() * .009m), 2);
                var low = Math.Max(.01m, Math.Round(Math.Min(open, close) * (1m - (decimal)random.NextDouble() * .009m), 2));
                await using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO MarketCandles(Symbol,CandleTimeUtc,[Open],High,Low,[Close],Volume) VALUES(@Symbol,@Time,@Open,@High,@Low,@Close,@Volume)";
                insert.Parameters.AddWithValue("@Symbol", instrument.Key);
                insert.Parameters.AddWithValue("@Time", date);
                insert.Parameters.AddWithValue("@Open", open);
                insert.Parameters.AddWithValue("@High", high);
                insert.Parameters.AddWithValue("@Low", low);
                insert.Parameters.AddWithValue("@Close", close);
                insert.Parameters.AddWithValue("@Volume", random.NextInt64(125_000, 4_500_000));
                await insert.ExecuteNonQueryAsync();
                price = close;
            }
        }
        await transaction.CommitAsync();
    }

    private async Task SeedUserAsync(SqlConnection connection, string name, string email, string password, string role)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF NOT EXISTS (SELECT 1 FROM AppUsers WHERE Email = @Email)
                INSERT INTO AppUsers (FullName, Email, PasswordHash, Role)
                VALUES (@FullName, @Email, @PasswordHash, @Role);
            """;
        command.Parameters.AddWithValue("@FullName", name);
        command.Parameters.AddWithValue("@Email", email);
        command.Parameters.AddWithValue("@PasswordHash", passwords.Hash(password));
        command.Parameters.AddWithValue("@Role", role);
        await command.ExecuteNonQueryAsync();
    }

    private const string SchemaSql = """
        IF OBJECT_ID('AppUsers') IS NULL
        CREATE TABLE AppUsers (
            Id INT IDENTITY PRIMARY KEY, FullName NVARCHAR(120) NOT NULL,
            Email NVARCHAR(200) NOT NULL UNIQUE, PasswordHash NVARCHAR(300) NOT NULL,
            Role NVARCHAR(20) NOT NULL, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());

        IF OBJECT_ID('RiskProfiles') IS NULL
        CREATE TABLE RiskProfiles (
            Id INT IDENTITY PRIMARY KEY, UserId INT NOT NULL,
            Capital DECIMAL(18,2) NOT NULL, TradingStyle INT NOT NULL,
            RiskPerTradePercent DECIMAL(5,2) NOT NULL,
            MaxTotalRiskPercent DECIMAL(5,2) NOT NULL,
            MinimumRewardRiskRatio DECIMAL(5,2) NOT NULL,
            IsActive BIT NOT NULL DEFAULT 1, UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
            CONSTRAINT FK_RiskProfiles_Users FOREIGN KEY(UserId) REFERENCES AppUsers(Id),
            CONSTRAINT UQ_RiskProfiles_UserStyle UNIQUE(UserId, TradingStyle));

        IF OBJECT_ID('TradingAccounts') IS NULL
        CREATE TABLE TradingAccounts (
            UserId INT NOT NULL PRIMARY KEY,
            Capital DECIMAL(18,2) NOT NULL,
            UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
            CONSTRAINT FK_TradingAccounts_Users FOREIGN KEY(UserId) REFERENCES AppUsers(Id));

        IF OBJECT_ID('ResearchSignals') IS NULL
        CREATE TABLE ResearchSignals (
            Id INT IDENTITY PRIMARY KEY, Symbol NVARCHAR(30) NOT NULL, Side INT NOT NULL,
            TradingStyle INT NOT NULL, EntryPrice DECIMAL(18,2) NOT NULL,
            StopLoss DECIMAL(18,2) NOT NULL, TargetPrice DECIMAL(18,2) NOT NULL,
            AiNewsSummary NVARCHAR(1000) NOT NULL, ValidFromUtc DATETIME2 NOT NULL,
            ValidUntilUtc DATETIME2 NOT NULL, IsActive BIT NOT NULL DEFAULT 1,
            CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());

        IF OBJECT_ID('TradeOrders') IS NULL
        CREATE TABLE TradeOrders (
            Id INT IDENTITY PRIMARY KEY, UserId INT NOT NULL, SignalId INT NOT NULL,
            Symbol NVARCHAR(30) NOT NULL, Side INT NOT NULL,
            EntryPrice DECIMAL(18,2) NOT NULL, StopLoss DECIMAL(18,2) NOT NULL,
            TargetPrice DECIMAL(18,2) NOT NULL, Quantity INT NOT NULL,
            RiskAmount DECIMAL(18,2) NOT NULL, ValidUntilUtc DATETIME2 NOT NULL,
            Status INT NOT NULL, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
            ExecutedUtc DATETIME2 NULL,
            CONSTRAINT FK_Orders_Users FOREIGN KEY(UserId) REFERENCES AppUsers(Id),
            CONSTRAINT FK_Orders_Signals FOREIGN KEY(SignalId) REFERENCES ResearchSignals(Id));

        IF OBJECT_ID('MarketCandles') IS NULL
        CREATE TABLE MarketCandles (
            Id BIGINT IDENTITY PRIMARY KEY, Symbol NVARCHAR(30) NOT NULL,
            CandleTimeUtc DATETIME2 NOT NULL, [Open] DECIMAL(18,2) NOT NULL,
            High DECIMAL(18,2) NOT NULL, Low DECIMAL(18,2) NOT NULL,
            [Close] DECIMAL(18,2) NOT NULL, Volume BIGINT NOT NULL,
            CONSTRAINT UQ_MarketCandles_SymbolTime UNIQUE(Symbol,CandleTimeUtc));
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_MarketCandles_SymbolTime')
            CREATE INDEX IX_MarketCandles_SymbolTime ON MarketCandles(Symbol,CandleTimeUtc DESC);
        """;

}
