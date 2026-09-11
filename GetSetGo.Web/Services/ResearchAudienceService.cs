using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using Microsoft.Data.SqlClient;

namespace GetSetGo.Web.Services;

public sealed class ResearchAudienceService(ISqlConnectionFactory factory, IPasswordService passwords)
{
    public async Task<object> Preview(int style, decimal entry, decimal stop, decimal target, int side)
    {
        if (!Enum.IsDefined(typeof(TradingStyle), style) || !Enum.IsDefined(typeof(SignalSide), side) ||
            entry <= 0 || stop <= 0 || target <= 0 ||
            !(side == 1 ? stop < entry && entry < target : target < entry && entry < stop))
            throw new ArgumentException("Enter valid style, side and price levels.");
        var ratio = Math.Abs(target-entry)/Math.Abs(entry-stop);
        await using var c=factory.Create(); await c.OpenAsync();
        await using var cmd=c.CreateCommand();
        cmd.CommandText="""
            SELECT COUNT(*) FROM AppUsers WHERE Role='Client';
            SELECT u.FullName,u.Email,r.Capital,r.MinimumRewardRiskRatio
            FROM AppUsers u JOIN RiskProfiles r ON r.UserId=u.Id
            WHERE u.Role='Client' AND r.IsActive=1 AND r.Capital>0
            AND r.TradingStyle=@Style AND r.MinimumRewardRiskRatio<=@Ratio
            ORDER BY r.MinimumRewardRiskRatio,u.FullName;
            """;
        cmd.Parameters.AddWithValue("@Style",style);cmd.Parameters.AddWithValue("@Ratio",ratio);
        await using var reader=await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();var total=reader.GetInt32(0);
        await reader.NextResultAsync();
        var clients=new List<object>();
        while(await reader.ReadAsync()) clients.Add(new {name=reader.GetString(0),email=reader.GetString(1),capital=reader.GetDecimal(2),minimumRatio=reader.GetDecimal(3)});
        return new {total,eligible=clients.Count,ratio,style=((TradingStyle)style).ToString(),clients};
    }

    public async Task Seed()
    {
        await using var c=factory.Create();await c.OpenAsync();
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        // Independent fixture identities; existing clients and previously seeded rules are untouched.
        for(var i=1;i<=200;i++)
        {
            await using var cmd=c.CreateCommand();cmd.Transaction=tx;
            cmd.CommandText="""
                DECLARE @User int;
                SELECT @User=Id FROM AppUsers WITH(UPDLOCK,HOLDLOCK) WHERE Email=@Email;
                IF @User IS NULL BEGIN
                    INSERT AppUsers(FullName,Email,PasswordHash,Role) VALUES(@Name,@Email,@Hash,'Client');
                    SET @User=SCOPE_IDENTITY();
                    INSERT TradingAccounts(UserId,Capital) VALUES(@User,100000);
                    INSERT RiskProfiles(UserId,Capital,TradingStyle,RiskPerTradePercent,MaxTotalRiskPercent,MinimumRewardRiskRatio,IsActive)
                    VALUES(@User,@Capital,@Style,1,3,@Ratio,1);
                END
                """;
            cmd.Parameters.AddWithValue("@Email",$"audience-demo-{i:000}@getsetgo.local");
            cmd.Parameters.AddWithValue("@Name",$"Demo audience client {i:000}");
            cmd.Parameters.AddWithValue("@Hash",passwords.Hash(Guid.NewGuid().ToString("N")));
            cmd.Parameters.AddWithValue("@Style",(i-1)%4+1);
            cmd.Parameters.AddWithValue("@Ratio",new decimal[]{1,1.5m,2,2.5m,3,3.1m,3.5m,4,4.5m,5}[((i-1)/4)%10]);
            cmd.Parameters.AddWithValue("@Capital",10000+((i-1)/4%5)*10000);
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }
}