namespace Clean_Connect.Application.DTO
{
    public class WorkerBankDetailDto
    {
        public bool HasDetails { get; set; }
        public string BankCode { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string Currency { get; set; } = "NGN";

        public string MaskedAccountNumber =>
            string.IsNullOrEmpty(AccountNumber) || AccountNumber.Length < 4
                ? AccountNumber
                : new string('•', AccountNumber.Length - 4) + AccountNumber[^4..];
    }
}