namespace OrderProcessing.Application.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Sales = "Sales";
    public const string Warehouse = "Warehouse";

    public static readonly string[] All = [Admin, Manager, Sales, Warehouse];
}

public static class Policies
{
    public const string CustomersRead = "CustomersRead";
    public const string CustomersWrite = "CustomersWrite";
    public const string ProductsRead = "ProductsRead";
    public const string ProductsManage = "ProductsManage";
    public const string OrdersRead = "OrdersRead";
    public const string OrdersCreate = "OrdersCreate";
    public const string OrdersEdit = "OrdersEdit";
    public const string InventoryRead = "InventoryRead";
}

public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    public string Currency { get; set; } = "USD";

    public decimal TaxRate { get; set; } = 0.08m;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
