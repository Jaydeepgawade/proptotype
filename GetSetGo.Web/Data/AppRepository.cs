using GetSetGo.Web.Models;
using Microsoft.Data.SqlClient;

namespace GetSetGo.Web.Data;

public interface IAppRepository
{
    Task<AppUser?> FindUserAsync(string email);
    Task<RiskProfile?> GetRiskProfileAsync(int userId);
    Task<RiskProfile?> GetRiskProfileAsync(int userId, TradingStyle style);
    Task<IReadOnlyList<RiskProfile>> GetRiskProfilesAsync(int userId);
    Task<decimal> GetAccountCapitalAsync(int userId);
    Task SaveRiskSetupAsync(int userId, decimal totalCapital, IEnumerable<RiskProfile> profiles);
    Task UpsertRiskProfileAsync(RiskProfile profile);
    Task<IReadOnlyList<ResearchSignal>> GetMatchingSignalsAsync(int userId);
    Task<IReadOnlyList<ResearchSignal>> GetAllSignalsAsync();
    Task<ResearchSignal?> GetSignalAsync(int id);
    Task AddSignalAsync(ResearchSignal signal);
    Task<int> AddOrderAsync(TradeOrder order);
    Task<bool> HasActiveOrderAsync(int userId, int signalId);
    Task<TradeOrder?> GetOrderAsync(int id, int userId);
    Task<IReadOnlyList<TradeOrder>> GetOrdersAsync(int userId);
    Task UpdateOrderStatusAsync(int id, int userId, OrderStatus status);
    Task<decimal> GetActiveRiskAsync(int userId);
    Task<IReadOnlyList<MarketCandle>> GetMarketCandlesAsync(string symbol, int take);
}

public sealed class AppRepository(ISqlConnectionFactory factory) : IAppRepository
{
    public async Task<AppUser?> FindUserAsync(string email)
    {
        await using var c = factory.Create(); await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT TOP 1 Id,FullName,Email,PasswordHash,Role FROM AppUsers WHERE Email=@Email";
        cmd.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? new AppUser { Id=r.GetInt32(0), FullName=r.GetString(1), Email=r.GetString(2), PasswordHash=r.GetString(3), Role=r.GetString(4) } : null;
    }

    public async Task<RiskProfile?> GetRiskProfileAsync(int userId)
    {
        await using var c = factory.Create(); await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT TOP 1 Id,UserId,Capital,TradingStyle,RiskPerTradePercent,MaxTotalRiskPercent,MinimumRewardRiskRatio,IsActive FROM RiskProfiles WHERE UserId=@UserId AND IsActive=1 ORDER BY UpdatedUtc DESC";
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadRisk(r) : null;
    }

    public async Task<RiskProfile?> GetRiskProfileAsync(int userId, TradingStyle style)
    {
        await using var c = factory.Create(); await c.OpenAsync(); await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT TOP 1 Id,UserId,Capital,TradingStyle,RiskPerTradePercent,MaxTotalRiskPercent,MinimumRewardRiskRatio,IsActive FROM RiskProfiles WHERE UserId=@UserId AND TradingStyle=@Style AND IsActive=1";
        Add(cmd, "@UserId", userId); Add(cmd, "@Style", (int)style);
        await using var r = await cmd.ExecuteReaderAsync(); return await r.ReadAsync() ? ReadRisk(r) : null;
    }

    public async Task<IReadOnlyList<RiskProfile>> GetRiskProfilesAsync(int userId)
    {
        var profiles = new List<RiskProfile>(); await using var c = factory.Create(); await c.OpenAsync(); await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id,UserId,Capital,TradingStyle,RiskPerTradePercent,MaxTotalRiskPercent,MinimumRewardRiskRatio,IsActive FROM RiskProfiles WHERE UserId=@UserId AND IsActive=1 ORDER BY TradingStyle";
        Add(cmd, "@UserId", userId); await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) profiles.Add(ReadRisk(r)); return profiles;
    }

    public async Task<decimal> GetAccountCapitalAsync(int userId)
    {
        await using var c = factory.Create(); await c.OpenAsync(); await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COALESCE((SELECT Capital FROM TradingAccounts WHERE UserId=@UserId), 0)";
        Add(cmd, "@UserId", userId); return Convert.ToDecimal(await cmd.ExecuteScalarAsync());
    }

    public async Task SaveRiskSetupAsync(int userId, decimal totalCapital, IEnumerable<RiskProfile> profiles)
    {
        await using var c = factory.Create(); await c.OpenAsync(); await using var transaction = (SqlTransaction)await c.BeginTransactionAsync();
        try
        {
            await using (var account = c.CreateCommand())
            {
                account.Transaction = transaction;
                account.CommandText = "MERGE TradingAccounts AS t USING (SELECT @UserId UserId) AS s ON t.UserId=s.UserId WHEN MATCHED THEN UPDATE SET Capital=@Capital,UpdatedUtc=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(UserId,Capital) VALUES(@UserId,@Capital);";
                Add(account, "@UserId", userId); Add(account, "@Capital", totalCapital); await account.ExecuteNonQueryAsync();
            }
            foreach (var profile in profiles)
            {
                await using var command = c.CreateCommand(); command.Transaction = transaction;
                command.CommandText = "MERGE RiskProfiles AS t USING (SELECT @UserId UserId,@Style TradingStyle) AS s ON t.UserId=s.UserId AND t.TradingStyle=s.TradingStyle WHEN MATCHED THEN UPDATE SET Capital=@Capital,RiskPerTradePercent=@Risk,MaxTotalRiskPercent=@MaxRisk,MinimumRewardRiskRatio=@Ratio,IsActive=1,UpdatedUtc=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(UserId,Capital,TradingStyle,RiskPerTradePercent,MaxTotalRiskPercent,MinimumRewardRiskRatio,IsActive) VALUES(@UserId,@Capital,@Style,@Risk,@MaxRisk,@Ratio,1);";
                Add(command, "@UserId", userId); Add(command, "@Capital", profile.Capital); Add(command, "@Style", (int)profile.TradingStyle);
                Add(command, "@Risk", profile.RiskPerTradePercent); Add(command, "@MaxRisk", profile.MaxTotalRiskPercent); Add(command, "@Ratio", profile.MinimumRewardRiskRatio);
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    public async Task UpsertRiskProfileAsync(RiskProfile p)
    {
        await using var c = factory.Create(); await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = """
            MERGE RiskProfiles AS t USING (SELECT @UserId UserId,@Style TradingStyle) AS s
            ON t.UserId=s.UserId AND t.TradingStyle=s.TradingStyle
            WHEN MATCHED THEN UPDATE SET Capital=@Capital,RiskPerTradePercent=@Risk,MaxTotalRiskPercent=@MaxRisk,MinimumRewardRiskRatio=@Ratio,IsActive=1,UpdatedUtc=SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT(UserId,Capital,TradingStyle,RiskPerTradePercent,MaxTotalRiskPercent,MinimumRewardRiskRatio,IsActive)
            VALUES(@UserId,@Capital,@Style,@Risk,@MaxRisk,@Ratio,1);
            """;
        Add(cmd,"@UserId",p.UserId); Add(cmd,"@Capital",p.Capital); Add(cmd,"@Style",(int)p.TradingStyle);
        Add(cmd,"@Risk",p.RiskPerTradePercent); Add(cmd,"@MaxRisk",p.MaxTotalRiskPercent); Add(cmd,"@Ratio",p.MinimumRewardRiskRatio);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<ResearchSignal>> GetMatchingSignalsAsync(int userId)
    {
        var list = new List<ResearchSignal>();
        await using var c=factory.Create(); await c.OpenAsync(); await using var cmd=c.CreateCommand();
        cmd.CommandText="""
            SELECT s.Id,s.Symbol,s.Side,s.TradingStyle,s.EntryPrice,s.StopLoss,s.TargetPrice,s.AiNewsSummary,s.ValidFromUtc,s.ValidUntilUtc,s.IsActive
            FROM ResearchSignals s JOIN RiskProfiles r ON r.UserId=@UserId AND r.IsActive=1 AND r.TradingStyle=s.TradingStyle AND r.Capital>0
            WHERE s.IsActive=1 AND SYSUTCDATETIME() BETWEEN s.ValidFromUtc AND s.ValidUntilUtc
            AND (ABS(s.TargetPrice-s.EntryPrice)/NULLIF(ABS(s.EntryPrice-s.StopLoss),0)) >= r.MinimumRewardRiskRatio
            ORDER BY s.ValidUntilUtc;
            """;
        cmd.Parameters.AddWithValue("@UserId",userId); await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync()) list.Add(ReadSignal(r)); return list;
    }

    public async Task<IReadOnlyList<ResearchSignal>> GetAllSignalsAsync()
    {
        var list=new List<ResearchSignal>(); await using var c=factory.Create(); await c.OpenAsync(); await using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT Id,Symbol,Side,TradingStyle,EntryPrice,StopLoss,TargetPrice,AiNewsSummary,ValidFromUtc,ValidUntilUtc,IsActive FROM ResearchSignals ORDER BY Id DESC";
        await using var r=await cmd.ExecuteReaderAsync(); while(await r.ReadAsync()) list.Add(ReadSignal(r)); return list;
    }

    public async Task<ResearchSignal?> GetSignalAsync(int id)
    {
        await using var c=factory.Create(); await c.OpenAsync(); await using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT Id,Symbol,Side,TradingStyle,EntryPrice,StopLoss,TargetPrice,AiNewsSummary,ValidFromUtc,ValidUntilUtc,IsActive FROM ResearchSignals WHERE Id=@Id";
        cmd.Parameters.AddWithValue("@Id",id); await using var r=await cmd.ExecuteReaderAsync(); return await r.ReadAsync()?ReadSignal(r):null;
    }

    public async Task AddSignalAsync(ResearchSignal s)
    {
        await using var c=factory.Create(); await c.OpenAsync(); await using var cmd=c.CreateCommand();
        cmd.CommandText="""INSERT INTO ResearchSignals(Symbol,Side,TradingStyle,EntryPrice,StopLoss,TargetPrice,AiNewsSummary,ValidFromUtc,ValidUntilUtc,IsActive) VALUES(@Symbol,@Side,@Style,@Entry,@Stop,@Target,@News,@From,@Until,1)""";
        Add(cmd,"@Symbol",s.Symbol.Trim().ToUpperInvariant()); Add(cmd,"@Side",(int)s.Side); Add(cmd,"@Style",(int)s.TradingStyle); Add(cmd,"@Entry",s.EntryPrice); Add(cmd,"@Stop",s.StopLoss); Add(cmd,"@Target",s.TargetPrice); Add(cmd,"@News",s.AiNewsSummary); Add(cmd,"@From",s.ValidFromUtc); Add(cmd,"@Until",s.ValidUntilUtc);
        await cmd.ExecuteNonQueryAsync();
        await DatabaseInitializer.SeedMarketCandlesAsync(c);
    }

    public async Task<int> AddOrderAsync(TradeOrder o)
    {
        await using var c=factory.Create(); await c.OpenAsync(); await using var cmd=c.CreateCommand();
        cmd.CommandText="""INSERT INTO TradeOrders(UserId,SignalId,Symbol,Side,EntryPrice,StopLoss,TargetPrice,Quantity,RiskAmount,ValidUntilUtc,Status) OUTPUT INSERTED.Id VALUES(@User,@Signal,@Symbol,@Side,@Entry,@Stop,@Target,@Qty,@Risk,@Until,@Status)""";
        Add(cmd,"@User",o.UserId); Add(cmd,"@Signal",o.SignalId); Add(cmd,"@Symbol",o.Symbol); Add(cmd,"@Side",(int)o.Side); Add(cmd,"@Entry",o.EntryPrice); Add(cmd,"@Stop",o.StopLoss); Add(cmd,"@Target",o.TargetPrice); Add(cmd,"@Qty",o.Quantity); Add(cmd,"@Risk",o.RiskAmount); Add(cmd,"@Until",o.ValidUntilUtc); Add(cmd,"@Status",(int)o.Status);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<bool> HasActiveOrderAsync(int userId,int signalId)
    {
        await using var c=factory.Create(); await c.OpenAsync(); await using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT CASE WHEN EXISTS(SELECT 1 FROM TradeOrders WHERE UserId=@User AND SignalId=@Signal AND Status IN (1,2)) THEN 1 ELSE 0 END";
        Add(cmd,"@User",userId); Add(cmd,"@Signal",signalId); return Convert.ToInt32(await cmd.ExecuteScalarAsync())==1;
    }

    public async Task<TradeOrder?> GetOrderAsync(int id,int userId)
    {
        await using var c=factory.Create(); await c.OpenAsync(); await using var cmd=c.CreateCommand(); cmd.CommandText=OrderSelect+" WHERE Id=@Id AND UserId=@User"; Add(cmd,"@Id",id);Add(cmd,"@User",userId); await using var r=await cmd.ExecuteReaderAsync(); return await r.ReadAsync()?ReadOrder(r):null;
    }

    public async Task<IReadOnlyList<TradeOrder>> GetOrdersAsync(int userId)
    {
        var list=new List<TradeOrder>(); await using var c=factory.Create(); await c.OpenAsync();
        await using(var expire=c.CreateCommand()){expire.CommandText="UPDATE TradeOrders SET Status=3 WHERE UserId=@User AND Status=1 AND ValidUntilUtc<SYSUTCDATETIME()";Add(expire,"@User",userId);await expire.ExecuteNonQueryAsync();}
        await using var cmd=c.CreateCommand(); cmd.CommandText=OrderSelect+" WHERE UserId=@User ORDER BY Id DESC"; Add(cmd,"@User",userId); await using var r=await cmd.ExecuteReaderAsync(); while(await r.ReadAsync())list.Add(ReadOrder(r)); return list;
    }

    public async Task UpdateOrderStatusAsync(int id,int userId,OrderStatus status)
    {
        await using var c=factory.Create(); await c.OpenAsync(); await using var cmd=c.CreateCommand(); cmd.CommandText="UPDATE TradeOrders SET Status=@Status,ExecutedUtc=CASE WHEN @Status=2 THEN SYSUTCDATETIME() ELSE ExecutedUtc END WHERE Id=@Id AND UserId=@User"; Add(cmd,"@Status",(int)status);Add(cmd,"@Id",id);Add(cmd,"@User",userId); await cmd.ExecuteNonQueryAsync();
    }

    public async Task<decimal> GetActiveRiskAsync(int userId)
    {
        await using var c=factory.Create(); await c.OpenAsync(); await using var cmd=c.CreateCommand(); cmd.CommandText="SELECT COALESCE(SUM(RiskAmount),0) FROM TradeOrders WHERE UserId=@User AND Status IN (1,2)";Add(cmd,"@User",userId); return Convert.ToDecimal(await cmd.ExecuteScalarAsync());
    }

    public async Task<IReadOnlyList<MarketCandle>> GetMarketCandlesAsync(string symbol,int take)
    {
        var list=new List<MarketCandle>(); await using var c=factory.Create(); await c.OpenAsync(); await using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT TOP (@Take) Symbol,CandleTimeUtc,[Open],High,Low,[Close],Volume FROM MarketCandles WHERE Symbol=@Symbol ORDER BY CandleTimeUtc DESC";
        Add(cmd,"@Take",Math.Clamp(take,10,200)); Add(cmd,"@Symbol",symbol.Trim().ToUpperInvariant());
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())list.Add(new MarketCandle{Symbol=r.GetString(0),TimeUtc=r.GetDateTime(1),Open=r.GetDecimal(2),High=r.GetDecimal(3),Low=r.GetDecimal(4),Close=r.GetDecimal(5),Volume=r.GetInt64(6)});
        list.Reverse(); return list;
    }

    private static RiskProfile ReadRisk(SqlDataReader r)=>new(){Id=r.GetInt32(0),UserId=r.GetInt32(1),Capital=r.GetDecimal(2),TradingStyle=(TradingStyle)r.GetInt32(3),RiskPerTradePercent=r.GetDecimal(4),MaxTotalRiskPercent=r.GetDecimal(5),MinimumRewardRiskRatio=r.GetDecimal(6),IsActive=r.GetBoolean(7)};
    private static ResearchSignal ReadSignal(SqlDataReader r)=>new(){Id=r.GetInt32(0),Symbol=r.GetString(1),Side=(SignalSide)r.GetInt32(2),TradingStyle=(TradingStyle)r.GetInt32(3),EntryPrice=r.GetDecimal(4),StopLoss=r.GetDecimal(5),TargetPrice=r.GetDecimal(6),AiNewsSummary=r.GetString(7),ValidFromUtc=r.GetDateTime(8),ValidUntilUtc=r.GetDateTime(9),IsActive=r.GetBoolean(10)};
    private static TradeOrder ReadOrder(SqlDataReader r)=>new(){Id=r.GetInt32(0),UserId=r.GetInt32(1),SignalId=r.GetInt32(2),Symbol=r.GetString(3),Side=(SignalSide)r.GetInt32(4),EntryPrice=r.GetDecimal(5),StopLoss=r.GetDecimal(6),TargetPrice=r.GetDecimal(7),Quantity=r.GetInt32(8),RiskAmount=r.GetDecimal(9),ValidUntilUtc=r.GetDateTime(10),Status=(OrderStatus)r.GetInt32(11),CreatedUtc=r.GetDateTime(12),ExecutedUtc=r.IsDBNull(13)?null:r.GetDateTime(13),TradingStyle=r.IsDBNull(14)?null:(TradingStyle)r.GetInt32(14)};
    private static void Add(SqlCommand c,string name,object value)=>c.Parameters.AddWithValue(name,value);
    private const string OrderSelect="SELECT Id,UserId,SignalId,Symbol,Side,EntryPrice,StopLoss,TargetPrice,Quantity,RiskAmount,ValidUntilUtc,Status,CreatedUtc,ExecutedUtc,(SELECT s.TradingStyle FROM ResearchSignals s WHERE s.Id=TradeOrders.SignalId) AS TradingStyle FROM TradeOrders";
}
