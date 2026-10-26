namespace Hallagi.Api.Domain;

public enum UserRole { SuperAdmin, Owner, BranchManager, Cashier, Barber }
public enum MemberStatus { Active, Disabled }
public enum EntityStatus { Active, Inactive }
public enum CompensationType { FixedSalary, Commission, FixedPlusCommission, ChairRental }
public enum PaymentMethod { Cash, Card }
public enum SaleStatus { Completed, Cancelled }
public enum ExpenseCategory { Rent, ElectricityWater, Supplies, Maintenance, Salaries, Commissions, Other }
public enum ExpenseSource { CashDrawer, Bank, Other }
public enum ShiftStatus { Open, Closed }
public enum CashMovementType { Opening, Sale, SaleVoid, Expense, ManualIn, ManualOut }
public enum ExpenseStatus { Active, Voided }

// ===== Commerce: products, discounts, loyalty =====
public enum SaleItemType { Service, Product }
public enum DiscountType { Percent, Fixed }
public enum StockMovementType { Purchase, Sale, SaleVoid, Adjustment }
public enum LoyaltyTxType { Earn, Redeem, Reverse, Adjust }

// ===== Booking =====
public enum AppointmentStatus { Pending, Confirmed, Waiting, InService, Completed, Cancelled, NoShow }
public enum AppointmentSource { Staff, Online, WalkIn }
