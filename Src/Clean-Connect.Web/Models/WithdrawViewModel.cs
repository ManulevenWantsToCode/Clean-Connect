using System.ComponentModel.DataAnnotations;

namespace Clean_Connect.Web.Models
{
    public class WithdrawViewModel
    {
        public decimal WalletBalance { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Account number is required.")]
        [StringLength(16, ErrorMessage = "Account number is too long.")]
        public string? AccountNumber { get; set; }

        [Required(ErrorMessage = "Bank is required.")]
        public string? BankCode { get; set; }

        [Required(ErrorMessage = "Account name is required.")]
        public string? AccountName { get; set; }

        public string Currency { get; set; } = "NGN";
    }
}
