namespace OpenBudget.Api;

// Contract (phone/device) terms. DeviceTotal is the device price spread over the contract; a discount replaces
// the monthly contract (service) cost for the first DiscountMonths months.
public sealed record ContractTermsRequest(int Months, DateOnly StartDate, bool IncludesDevice, decimal UpfrontCost, decimal MonthlyServiceCost,
    decimal DeviceTotal, int DiscountMonths = 0, decimal? DiscountedServiceCost = null);

// A category created and paired in the same command. Exactly one of GroupId (an existing active, unprotected group)
// or NewGroupName (a group created by the command) is set.
public sealed record NewPairedCategoryRequest(string Name, string? GroupId = null, string? NewGroupName = null);

// Loan terms. InterestRate is an annual percentage (0-100, three decimals). MinimumPayment includes EscrowAmount.
// Pairing (per slot, at most one of): PairedCategoryId pairs an existing active category; NewPairedCategory creates a
// category (and optionally its group) in every initialized month and pairs it; neither means unpaired.
// Contracts with a device may set SeparateDeviceCategory: the device share of each payment then uses the device slot
// (DevicePairedCategoryId / NewDevicePairedCategory, or Ready to Assign when that slot is unpaired).
public sealed record LoanTermsRequest(decimal InterestRate, decimal MinimumPayment, decimal EscrowAmount = 0, string? PairedCategoryId = null,
    ContractTermsRequest? Contract = null, NewPairedCategoryRequest? NewPairedCategory = null, bool SeparateDeviceCategory = false,
    string? DevicePairedCategoryId = null, NewPairedCategoryRequest? NewDevicePairedCategory = null);

public sealed record SaveLoanTermsRequest(Guid CommandId, long Version, LoanTermsRequest Terms);
// Without Balance the current balance is confirmed; with a different Balance an adjustment transaction is recorded too.
public sealed record LoanBalanceRequest(Guid CommandId, long Version, decimal? Balance = null);
// Records the month's actual interest charge (positive amount, replacing any earlier one); amount 0 removes it so the estimate shows again.
public sealed record LoanInterestRequest(Guid CommandId, long Version, string Month, decimal Amount);

public sealed record LoanPairedCategoryDto(string Id, string Name, string GroupName, decimal Available, bool IsArchived, bool IsAutomatic,
    string? TargetKind, decimal? TargetMoreNeeded);
// One paired slot of a loan: Slot is "loan" (the account's main category) or "device" (a contract's separate device category).
public sealed record LoanPairingDto(Guid AccountId, string AccountName, string CategoryId, string Slot);
public sealed record LoanPairingsDto(List<LoanPairingDto> Items);
public sealed record LoanBalancePointDto(string Month, decimal Balance);
public sealed record LoanActivityItemDto(Guid Id, DateOnly Date, decimal Amount, string Kind, string Description, string? AccountName, Guid? AccountId = null);
// EndBalance is the ledger balance after the month (negative = debt). EstimatedInterest is the interest estimate on the
// previous month's debt; it is shown as an estimate and never posted.
public sealed record LoanActivityMonthDto(string Month, decimal EndBalance, decimal EstimatedInterest, List<LoanActivityItemDto> Payments, List<LoanActivityItemDto> Other, LoanActivityItemDto? Interest = null);
public sealed record LoanProjectionDto(decimal MonthlyPayment, int? PaymentsRemaining, string? PayoffMonth, decimal InterestRemaining,
    decimal PrincipalRemaining, decimal RemainingToPay, bool NeverPaidOff, List<LoanBalancePointDto> Track);
public sealed record ContractScheduleItemDto(int Number, DateOnly DueDate, decimal Service, decimal Device, decimal Total, bool IsDiscounted, bool IsPast);
public sealed record ContractDto(int Months, DateOnly StartDate, DateOnly EndDate, bool IncludesDevice, decimal UpfrontCost, decimal MonthlyServiceCost,
    decimal DeviceTotal, int DiscountMonths, decimal? DiscountedServiceCost, decimal TotalCost, decimal RemainingDevice, decimal RemainingService,
    decimal RemainingCommitment, int RemainingMonths, List<ContractScheduleItemDto> Schedule);
public sealed record LoanDto(Guid AccountId, string AccountType, long Version, string CurrentMonth, decimal Balance, decimal InterestRate,
    decimal MinimumPayment, decimal EscrowAmount, decimal OriginalBalance, decimal? PercentRemaining, DateTimeOffset? BalanceConfirmedAt,
    decimal EstimatedInterestThisMonth, LoanPairedCategoryDto? PairedCategory, LoanProjectionDto Projection, List<LoanBalancePointDto> History,
    List<LoanActivityMonthDto> Activity, ContractDto? Contract, bool SeparateDeviceCategory = false, LoanPairedCategoryDto? DevicePairedCategory = null);
public sealed record LoanSimulationDto(Guid AccountId, string CurrentMonth, decimal Balance, decimal MinimumPayment, decimal Extra, LoanProjectionDto Projection);
