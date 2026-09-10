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
        if (string.IsNullOrWhiteSpace(databaseName) || databaseName.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            throw new InvalidOperationException("The database name may contain only letters, numbers, and underscores.");

        builder.InitialCatalog = "master";
        await using (var master = new SqlConnection(builder.ConnectionString))
        {
            await master.OpenAsync();
            await using var create = master.CreateCommand();
            create.CommandText = $"IF DB_ID(N'{databaseName}') IS NULL CREATE DATABASE [{databaseName}];";
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

        await using var seedSignals = connection.CreateCommand();
        seedSignals.CommandText = SignalSeedSql;
        await seedSignals.ExecuteNonQueryAsync();
        await SeedMarketCandlesAsync(connection);
    }

    private static async Task SeedMarketCandlesAsync(SqlConnection connection)
    {
        await using(var count=connection.CreateCommand())
        {
            count.CommandText="SELECT COUNT_BIG(1) FROM MarketCandles";
            if(Convert.ToInt64(await count.ExecuteScalarAsync())>0)return;
        }

        var instruments=new Dictionary<string,decimal>{{"RELIANCE",2820m},{"TCS",3975m},{"HDFCBANK",1590m},{"M&M",2980m}};
        var random=new Random(20260908);
        await using var transaction=await connection.BeginTransactionAsync();
        foreach(var instrument in instruments)
        {
            var price=instrument.Value;
            var date=DateTime.UtcNow.Date.AddDays(-85);
            for(var i=0;i<60;)
            {
                date=date.AddDays(1); if(date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)continue;
                var open=Math.Round(price+(decimal)(random.NextDouble()-.5)*price*.012m,2);
                var close=Math.Round(open+(decimal)(random.NextDouble()-.47)*price*.018m,2);
                var high=Math.Round(Math.Max(open,close)+(decimal)random.NextDouble()*price*.009m,2);
                var low=Math.Round(Math.Min(open,close)-(decimal)random.NextDouble()*price*.009m,2);
                var volume=random.NextInt64(350_000,4_500_000);
                await using var insert=connection.CreateCommand(); insert.Transaction=(SqlTransaction)transaction;
                insert.CommandText="INSERT INTO MarketCandles(Symbol,CandleTimeUtc,[Open],High,Low,[Close],Volume) VALUES(@Symbol,@Time,@Open,@High,@Low,@Close,@Volume)";
                insert.Parameters.AddWithValue("@Symbol",instrument.Key);insert.Parameters.AddWithValue("@Time",date);insert.Parameters.AddWithValue("@Open",open);insert.Parameters.AddWithValue("@High",high);insert.Parameters.AddWithValue("@Low",low);insert.Parameters.AddWithValue("@Close",close);insert.Parameters.AddWithValue("@Volume",volume);
                await insert.ExecuteNonQueryAsync(); price=close; i++;
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

    private const string SignalSeedSql = """
        IF NOT EXISTS (SELECT 1 FROM ResearchSignals)
        BEGIN
            INSERT INTO ResearchSignals(Symbol,Side,TradingStyle,EntryPrice,StopLoss,TargetPrice,AiNewsSummary,ValidFromUtc,ValidUntilUtc)
            VALUES
            ('RELIANCE',1,1,2950,2925,3025,'Large-cap energy and retail company. Review the latest company announcements before execution.',SYSUTCDATETIME(),DATEADD(day,2,SYSUTCDATETIME())),
            ('TCS',1,2,4180,4100,4380,'IT services leader. The signal follows an EOD momentum setup with controlled downside.',SYSUTCDATETIME(),DATEADD(day,5,SYSUTCDATETIME())),
            ('HDFCBANK',1,3,1720,1650,1930,'Private-sector bank. This positional setup uses a wider stop and a three-to-one reward-risk profile.',SYSUTCDATETIME(),DATEADD(day,10,SYSUTCDATETIME())),
            ('M&M',2,1,3100,3130,3010,'Automobile stock with an EOD sell setup. Confirm the market trend before setting the order.',SYSUTCDATETIME(),DATEADD(day,2,SYSUTCDATETIME()));
        END
        """;
}
