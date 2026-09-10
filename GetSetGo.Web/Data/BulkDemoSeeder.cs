using GetSetGo.Web.Services;
using Microsoft.Data.SqlClient;

namespace GetSetGo.Web.Data;

// Explicit, development-only fixture import; normal startup never calls this.
internal static class BulkDemoSeeder
{
    internal static async Task SeedAsync(ISqlConnectionFactory factory, IPasswordService passwords)
    {
        await using var connection = factory.Create();
        await connection.OpenAsync();
        await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = 120;
            command.Parameters.AddWithValue("@PasswordHash", passwords.Hash("DemoOps@123"));
            command.CommandText = """
                SET XACT_ABORT ON;
                DECLARE @UserId int = (SELECT Id FROM AppUsers WHERE Email='client@getsetgo.local');
                IF @UserId IS NULL THROW 50000, 'Demo client is missing.', 1;
                IF NOT EXISTS (SELECT 1 FROM AppUsers WHERE Email='operations-demo@getsetgo.local')
                    INSERT AppUsers(FullName,Email,PasswordHash,Role)
                    VALUES('Demo Operations','operations-demo@getsetgo.local',@PasswordHash,'Client');
                DECLARE @TestUser int=(SELECT Id FROM AppUsers WHERE Email='operations-demo@getsetgo.local');
                IF NOT EXISTS (SELECT 1 FROM RiskProfiles WHERE UserId=@TestUser)
                    INSERT RiskProfiles(UserId,Capital,TradingStyle,RiskPerTradePercent,MaxTotalRiskPercent,MinimumRewardRiskRatio)
                    VALUES(@TestUser,100000,1,1,3,2);
                DECLARE @i int=1, @Now datetime2=SYSUTCDATETIME();
                WHILE @i<=120
                BEGIN
                    DECLARE @Symbol nvarchar(30)=CONCAT('DEMO',RIGHT(CONCAT('000',@i),3));
                    DECLARE @Style int=1+((@i-1)%4), @Side int=1+((@i-1)%2), @Status int=1+((@i-1)%5);
                    DECLARE @Entry decimal(18,2)=100+@i*10, @Stop decimal(18,2), @Target decimal(18,2);
                    SET @Stop=@Entry+CASE WHEN @Side=1 THEN -1 ELSE 1 END;
                    SET @Target=@Entry+CASE WHEN @Side=1 THEN 3 ELSE -3 END;
                    DECLARE @Until datetime2=CASE WHEN @Status=3 THEN DATEADD(day,-1,@Now) ELSE DATEADD(day,30,@Now) END;
                    IF NOT EXISTS(SELECT 1 FROM ResearchSignals WHERE Symbol=@Symbol)
                        INSERT ResearchSignals(Symbol,Side,TradingStyle,EntryPrice,StopLoss,TargetPrice,AiNewsSummary,ValidFromUtc,ValidUntilUtc,IsActive)
                        VALUES(@Symbol,@Side,@Style,@Entry,@Stop,@Target,'[DUMMY DATA] Synthetic company and research fixture for table and trading workflow demonstrations.',DATEADD(day,-60,@Now),@Until,1);
                    DECLARE @SignalId int=(SELECT TOP 1 Id FROM ResearchSignals WHERE Symbol=@Symbol ORDER BY Id);
                    IF NOT EXISTS(SELECT 1 FROM TradeOrders WHERE UserId=@UserId AND SignalId=@SignalId)
                        INSERT TradeOrders(UserId,SignalId,Symbol,Side,EntryPrice,StopLoss,TargetPrice,Quantity,RiskAmount,ValidUntilUtc,Status,CreatedUtc,ExecutedUtc)
                        VALUES(@UserId,@SignalId,@Symbol,@Side,@Entry,@Stop,@Target,1+(@i%5),1+(@i%5),@Until,@Status,DATEADD(day,-2,@Now),CASE WHEN @Status IN(2,5) THEN DATEADD(day,-2,DATEADD(hour,1,@Now)) ELSE NULL END);
                    SET @i=@i+1;
                END;
                SELECT COUNT(*) FROM TradeOrders WHERE UserId=@UserId AND Symbol LIKE 'DEMO[0-9][0-9][0-9]';
                """;
            var count = await command.ExecuteScalarAsync();
            await transaction.CommitAsync();
            Console.WriteLine($"Bulk demo fixtures: {count} orders for demo client. Existing fixtures are retained on rerun.");
        }
        await DatabaseInitializer.SeedMarketCandlesAsync(connection);
        await using var verify = connection.CreateCommand();
        verify.CommandText = "SELECT COUNT(*) FROM MarketCandles WHERE Symbol LIKE 'DEMO[0-9][0-9][0-9]'";
        Console.WriteLine($"Bulk demo candles: {await verify.ExecuteScalarAsync()}");
    }
}
