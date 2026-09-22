using System.ComponentModel.DataAnnotations;

namespace Clean_Connect.Application.DTO
{
    public record SetupAdminDto
    {
        [Required]
        [Display(Name = "Admin Setup Key")]
        public string SetupKey { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(Password))]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}