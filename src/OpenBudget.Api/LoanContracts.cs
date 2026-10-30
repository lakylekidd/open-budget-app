namespace OpenBudget.Api;

// Contract (phone/device) terms. DeviceTotal is the device price spread over the contract; a discount replaces
// the monthly contract (service) cost for the first DiscountMonths months.
public sealed record ContractTermsRequest(int Months, DateOnly StartDate, bool IncludesDevice, decimal UpfrontCost, decimal MonthlyServiceCost,
    decimal DeviceTotal, int DiscountMonths = 0, decimal? DiscountedServiceCost = null);

// Loan terms. InterestRate is an annual percentage (0-100, three decimals). MinimumPayment includes EscrowAmount.
// Pairing: PairedCategoryId pairs an existing active category; CreatePairedCategory pairs the account's own category
// in the Loans group (created or restored); neither means unpaired.
public sealed record LoanTermsRequest(decimal InterestRate, decimal MinimumPayment, decimal EscrowAmount = 0, string? PairedCategoryId = null,
    bool CreatePairedCategory = false, ContractTermsRequest? Contract = null);

public sealed record SaveLoanTermsRequest(Guid CommandId, long Version, LoanTermsRequest Terms);
// Without Balance the current balance is confirmed; with a different Balance an adjustment transaction is recorded too.
public sealed record LoanBalanceRequest(Guid CommandId, long Version, decimal? Balance = null);

public sealed record LoanPairedCategoryDto(string Id, string Name, string GroupName, decimal Available, bool IsArchived, bool IsAutomatic,
    string? TargetKind, decimal? TargetMoreNeeded);
public sealed record LoanBalancePointDto(string Month, decimal Balance);
public sealed record LoanActivityItemDto(Guid Id, DateOnly Date, decimal Amount, string Kind, string Description, string? AccountName);
// EndBalance is the ledger balance after the month (negative = debt). EstimatedInterest is the interest estimate on the
// previous month's debt; it is shown as an estimate and never posted.
public sealed record LoanActivityMonthDto(string Month, decimal EndBalance, decimal EstimatedInterest, List<LoanActivityItemDto> Payments, List<LoanActivityItemDto> Other);
public sealed record LoanProjectionDto(decimal MonthlyPayment, int? PaymentsRemaining, string? PayoffMonth, decimal InterestRemaining,
    decimal PrincipalRemaining, decimal RemainingToPay, bool NeverPaidOff, List<LoanBalancePointDto> Track);
public sealed record ContractScheduleItemDto(int Number, DateOnly DueDate, decimal Service, decimal Device, decimal Total, bool IsDiscounted, bool IsPast);
public sealed record ContractDto(int Months, DateOnly StartDate, DateOnly EndDate, bool IncludesDevice, decimal UpfrontCost, decimal MonthlyServiceCost,
    decimal DeviceTotal, int DiscountMonths, decimal? DiscountedServiceCost, decimal TotalCost, decimal RemainingDevice, decimal RemainingService,
    decimal RemainingCommitment, int RemainingMonths, List<ContractScheduleItemDto> Schedule);
public sealed record LoanDto(Guid AccountId, string AccountType, long Version, string CurrentMonth, decimal Balance, decimal InterestRate,
    decimal MinimumPayment, decimal EscrowAmount, decimal OriginalBalance, decimal? PercentRemaining, DateTimeOffset? BalanceConfirmedAt,
    decimal EstimatedInterestThisMonth, LoanPairedCategoryDto? PairedCategory, LoanProjectionDto Projection, List<LoanBalancePointDto> History,
    List<LoanActivityMonthDto> Activity, ContractDto? Contract);
public sealed record LoanSimulationDto(Guid AccountId, string CurrentMonth, decimal Balance, decimal MinimumPayment, decimal Extra, LoanProjectionDto Projection);
