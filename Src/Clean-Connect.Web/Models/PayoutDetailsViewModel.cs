using System.ComponentModel.DataAnnotations;

namespace Clean_Connect.Web.Models
{
    public class PayoutDetailsViewModel
    {
        public bool HasDetails { get; set; }

        [Required(ErrorMessage = "Select your bank.")]
        [Display(Name = "Bank")]
        public string BankCode { get; set; } = string.Empty;

        public string BankName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your account number.")]
        [RegularExpression("^[0-9]{10}$", ErrorMessage = "Account number must be 10 digits.")]
        [Display(Name = "Account number")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm the account name before saving.")]
        [Display(Name = "Account name")]
        public string AccountName { get; set; } = string.Empty;

        public string? Currency { get; set; } = "NGN";
        public string? MaskedAccountNumber { get; set; }
    }
}