using Hallagi.Api.Domain;
using System.Text.Json;

namespace Hallagi.Api.Services;

public static class Permissions
{
    // ===== Action constants =====
    public const string SaleCreate = "sale.create";
    public const string SaleView = "sale.view";
    public const string SaleVoid = "sale.void";
    public const string CustomerManage = "customer.manage";
    public const string ServiceView = "service.view";
    public const string ServiceManage = "service.manage";
    public const string BarberView = "barber.view";
    public const string BarberManage = "barber.manage";
    public const string BarberDuesView = "barber.dues.view";
    public const string ExpenseCreate = "expense.create";
    public const string ExpenseView = "expense.view";
    public const string ExpenseVoid = "expense.void";
    public const string ShiftOpen = "shift.open";
    public const string ShiftClose = "shift.close";
    public const string ShiftView = "shift.view";
    public const string ReportView = "report.view";
    public const string BranchManage = "branch.manage";
    public const string UserManage = "user.manage";
    public const string SettingsManage = "settings.manage";
    public const string AllBranches = "branch.all";

    // Financial management
    public const string LedgerView = "ledger.view";
    public const string AdvanceCreate = "advance.create";
    public const string AdvanceApprove = "advance.approve";
    public const string HandoverCreate = "handover.create";
    public const string SettlementPreview = "settlement.preview";
    public const string SettlementApprove = "settlement.approve";
    public const string SettlementPay = "settlement.pay";
    public const string DeviceManage = "device.manage";
    public const string SubscriptionView = "subscription.view";
    public const string PlatformManage = "platform.manage";  // مالك المنصة فقط

    public static readonly string[] All = new[]
    {
        SaleCreate, SaleView, SaleVoid, CustomerManage, ServiceView, ServiceManage,
        BarberView, BarberManage, BarberDuesView, ExpenseCreate, ExpenseView, ExpenseVoid,
        ShiftOpen, ShiftClose, ShiftView, ReportView, BranchManage, UserManage, SettingsManage,
        LedgerView, AdvanceCreate, AdvanceApprove, HandoverCreate,
        SettlementPreview, SettlementApprove, SettlementPay, DeviceManage, SubscriptionView
    };

    // ===== Role default matrix =====
    private static readonly Dictionary<UserRole, HashSet<string>> RoleDefaults = new()
    {
        [UserRole.Owner] = new HashSet<string>(All.Concat(new[] { AllBranches })),
        [UserRole.BranchManager] = new HashSet<string>
        {
            SaleCreate, SaleView, CustomerManage, ServiceView, BarberView,
            BarberDuesView, ExpenseCreate, ExpenseView, ShiftOpen, ShiftClose, ShiftView, ReportView,
            LedgerView, AdvanceCreate, HandoverCreate, SettlementPreview, DeviceManage
        },
        [UserRole.Cashier] = new HashSet<string>
        {
            SaleCreate, SaleView, CustomerManage, ServiceView, BarberView,
            ShiftOpen, ShiftView, HandoverCreate
        },
        [UserRole.Barber] = new HashSet<string>
        {
            // Barber-operated POS: may record sales for HIMSELF only (enforced in SalesService)
            SaleCreate, SaleView, ServiceView, BarberDuesView
        }
    };

    /// <summary>Central permission check: can(user, action, resource).</summary>
    public static bool Can(CurrentUser user, string action)
    {
        if (user.Role == UserRole.SuperAdmin) return true; // platform owner owns everything

        var defaults = RoleDefaults.TryGetValue(user.Role, out var set) ? set : new HashSet<string>();
        bool value = defaults.Contains(action);

        // Member-level overrides (set by the owner) win over role defaults.
        var overrides = user.PermissionOverrides;
        if (overrides != null && overrides.TryGetValue(action, out var overrideValue))
            value = overrideValue;

        return value;
    }

    public static Dictionary<string, bool> EffectivePermissions(CurrentUser user)
    {
        var result = new Dictionary<string, bool>
        {
            [AllBranches] = AccessesAllBranches(user)
        };
        foreach (var action in All)
            result[action] = Can(user, action);
        return result;
    }

    public static Dictionary<string, bool>? ParseOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
            return dict;
        }
        catch { return null; }
    }

    /// <summary>True when the user may access all branches of the organization.</summary>
    public static bool AccessesAllBranches(CurrentUser user) => user.Role == UserRole.Owner || Can(user, AllBranches);

    /// <summary>True when the user may access the given branch.</summary>
    public static bool CanAccessBranch(CurrentUser user, Guid branchId)
        => AccessesAllBranches(user) || user.BranchIds.Contains(branchId);
}
