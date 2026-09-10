using System.ComponentModel.DataAnnotations;

namespace GetSetGo.Web.Models;

public sealed record ApiLoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record RiskProfileRequest(
    [Range(1000, 100000000)] decimal Capital,
    TradingStyle TradingStyle,
    [Range(0.1, 10)] decimal RiskPerTradePercent,
    [Range(0.1, 25)] decimal MaxTotalRiskPercent,
    [Range(1, 5)] decimal MinimumRewardRiskRatio);

public sealed record ResearchSignalRequest(
    [Required, StringLength(30)] string Symbol,
    SignalSide Side,
    TradingStyle TradingStyle,
    [Range(0.01, 10000000)] decimal EntryPrice,
    [Range(0.01, 10000000)] decimal StopLoss,
    [Range(0.01, 10000000)] decimal TargetPrice,
    [Required, StringLength(1000)] string AiNewsSummary,
    DateTime ValidFromUtc,
    DateTime ValidUntilUtc);
