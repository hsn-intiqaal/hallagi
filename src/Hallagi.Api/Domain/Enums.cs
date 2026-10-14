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
