using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Models
{
    public class Order
    {
        [Required(ErrorMessage = "Please enter the order amount.")]
        [Range(1, 10000, ErrorMessage = "Amount must be between 1 and 10,000.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Please select an order type.")]
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