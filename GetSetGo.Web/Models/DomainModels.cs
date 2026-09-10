using System.ComponentModel.DataAnnotations;

namespace GetSetGo.Web.Models;

public enum TradingStyle { Intraday = 1, Swing = 2, Positional = 3, ShortTermDelivery = 4 }
public enum SignalSide { Buy = 1, Sell = 2 }
public enum OrderStatus { Set = 1, Executed = 2, Expired = 3, Cancelled = 4, Closed = 5 }

public sealed class AppUser
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Client";
}

public sealed class RiskProfile : IValidatableObject
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [Range(0, 100000000)] public decimal Capital { get; set; }
    [Required] public TradingStyle TradingStyle { get; set; }
    [Range(0.1, 5)] public decimal RiskPerTradePercent { get; set; } = 1;
    [Range(0.1, 5)] public decimal MaxTotalRiskPercent { get; set; } = 3;
    [Range(0.1, 5)] public decimal MinimumRewardRiskRatio { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if(RiskPerTradePercent > MaxTotalRiskPercent)
        {
            yield return new ValidationResult("Risk per trade cannot be greater than Maximum total Risk", new[] { nameof(RiskPerTradePercent) });
        }
    }
}

public sealed class RiskSetupViewModel
{
    [Range(1000, 100000000)] public decimal TotalCapital { get; set; }
    public List<RiskProfile> Profiles { get; set; } = [];
}

public sealed class ResearchSignal
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string Symbol { get; set; } = "";
    public SignalSide Side { get; set; }
    public TradingStyle TradingStyle { get; set; }
    [Range(0.01, 10000000)] public decimal EntryPrice { get; set; }
    [Range(0.01, 10000000)] public decimal StopLoss { get; set; }
    [Range(0.01, 10000000)] public decimal TargetPrice { get; set; }
    [Required, StringLength(1000)] public string AiNewsSummary { get; set; } = "";
    public DateTime ValidFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime ValidUntilUtc { get; set; } = DateTime.UtcNow.AddDays(1);
    public bool IsActive { get; set; } = true;
    public decimal RewardRiskRatio => Math.Abs(EntryPrice - StopLoss) == 0 ? 0 :
        Math.Round(Math.Abs(TargetPrice - EntryPrice) / Math.Abs(EntryPrice - StopLoss), 2);
}

public sealed class TradeOrder
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int SignalId { get; set; }
    public TradingStyle? TradingStyle { get; set; }
    public string Symbol { get; set; } = "";
    public SignalSide Side { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TargetPrice { get; set; }
    public int Quantity { get; set; }
    public decimal RiskAmount { get; set; }
    public DateTime ValidUntilUtc { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? ExecutedUtc { get; set; }
}

public sealed class MarketCandle
{
    public string Symbol { get; set; } = "";
    public DateTime TimeUtc { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

public sealed class AppNotification
{
    public int Id { get; set; }
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class DashboardViewModel
{
    public RiskProfile? RiskProfile { get; set; }
    public int MatchingSignals { get; set; }
    public int SetOrders { get; set; }
    public int OpenTrades { get; set; }
    public decimal ActiveRiskAmount { get; set; }
}

public sealed class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
