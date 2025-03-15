namespace OrderProcessing.Models
{
    public class Order
    {
        public decimal Amount { get; set; }
        public string CustomerType { get; set; } // "New" or "Loyal"
        public decimal Discount => CalculateDiscount();
        public decimal FinalTotal => Amount - Discount;

        private decimal CalculateDiscount()
        {
            if (CustomerType == "Loyal" && Amount >= 100)
                return Amount * 0.10m; // 10% discount
            return 0m;
        }
    }
}