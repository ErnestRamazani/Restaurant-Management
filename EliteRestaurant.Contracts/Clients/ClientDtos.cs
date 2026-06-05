namespace EliteRestaurant.Contracts.Clients;

public sealed record RestaurantClientListItemDto(
    int Id,
    string UniqueId,
    string FullName,
    string PrimaryPhone,
    string Email,
    decimal DebtBalanceUsd,
    bool IsStaffClient,
    int? EmployeeId,
    bool IsActive);

public sealed record RestaurantClientDetailDto(
    int Id,
    string UniqueId,
    string FullName,
    string PrimaryPhone,
    string Email,
    string InternalNotes,
    decimal DebtBalanceUsd,
    decimal TotalSettledRevenueUsd,
    decimal TotalGeneratedRevenueUsd,
    bool IsStaffClient,
    int? EmployeeId,
    decimal? StaffMealDiscountPercent,
    bool IsActive);

public sealed record RestaurantClientSearchResultDto(
    int Id,
    string UniqueId,
    string FullName,
    string PrimaryPhone,
    bool IsStaffClient,
    decimal DebtBalanceUsd);

public sealed record CreateRestaurantClientRequest(
    string FullName,
    string? PrimaryPhone,
    string? Email,
    string? InternalNotes);

public sealed record UpdateRestaurantClientRequest(
    string FullName,
    string? PrimaryPhone,
    string? Email,
    string? InternalNotes,
    bool IsActive);

public sealed record LinkOrderToClientRequest(int RestaurantClientId);

public sealed record SettleClientDebtRequest(
    string Passcode,
    decimal PaymentAmountUsd,
    string? Note);

public sealed record SettleClientDebtResponse(
    bool Ok,
    string? Message,
    decimal RemainingDebtUsd,
    decimal AmountAppliedUsd);

public sealed record ClientOrderTicketDto(
    int OrderId,
    string OrderCode,
    DateTime CreatedAt,
    string Status,
    string ClientSettlement,
    decimal GrandTotalUsd,
    decimal AmountOnAccountUsd,
    decimal ClientDebtSettledUsd,
    bool RevenueRecognized);

public sealed record ClientLedgerEntryDto(
    int Id,
    string EntryType,
    decimal AmountUsd,
    decimal BalanceAfterUsd,
    string Note,
    DateTime CreatedAtUtc,
    int? OrderId,
    string? OrderCode);

public sealed record RestaurantClientProfileDto(
    RestaurantClientDetailDto Client,
    IReadOnlyList<ClientOrderTicketDto> Orders,
    IReadOnlyList<ClientLedgerEntryDto> Ledger);

public sealed record OrderClientLinkDto(
    int? RestaurantClientId,
    string? ClientUniqueId,
    string? ClientFullName,
    decimal DebtBalanceUsd,
    bool CanAddToDebt,
    decimal DebtCapUsd);
