using Clean_Connect.Domain.Utilities;

namespace Clean_Connect.Domain.Entities
{
    public class WorkerBankDetail : BaseEntity
    {
        private WorkerBankDetail() { }

        private WorkerBankDetail(Guid workerId, string bankCode, string bankName, string accountNumber, string accountName, string currency, string? createdBy = null)
        {
            ValidateWorkerId(workerId);
            ValidateBankCode(bankCode);
            ValidateAccountNumber(accountNumber);
            ValidateAccountName(accountName);

            WorkerId = workerId;
            BankCode = bankCode.Trim();
            BankName = bankName?.Trim() ?? string.Empty;
            AccountNumber = accountNumber.Trim();
            AccountName = accountName.Trim();
            Currency = string.IsNullOrWhiteSpace(currency) ? "NGN" : currency.Trim().ToUpperInvariant();
            IsActive = true;
            UpdateMetadata(createdBy);
        }

        public Guid WorkerId { get; private set; }
        public Worker Worker { get; private set; } = default!;
        public string BankCode { get; private set; } = default!;
        public string BankName { get; private set; } = string.Empty;
        public string AccountNumber { get; private set; } = default!;
        public string AccountName { get; private set; } = default!;
        public string Currency { get; private set; } = "NGN";
        public string? RecipientCode { get; private set; }
        public bool IsActive { get; private set; }

        public static WorkerBankDetail Create(Guid workerId, string bankCode, string bankName, string accountNumber, string accountName, string currency, string? createdBy = null)
        {
            return new WorkerBankDetail(workerId, bankCode, bankName, accountNumber, accountName, currency, createdBy);
        }

        public void Update(string bankCode, string bankName, string accountNumber, string accountName, string currency, string? modifiedBy = null)
        {
            ValidateBankCode(bankCode);
            ValidateAccountNumber(accountNumber);
            ValidateAccountName(accountName);

            var bankChanged = !string.Equals(BankCode, bankCode.Trim(), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(AccountNumber, accountNumber.Trim(), StringComparison.Ordinal);

            BankCode = bankCode.Trim();
            BankName = bankName?.Trim() ?? string.Empty;
            AccountNumber = accountNumber.Trim();
            AccountName = accountName.Trim();
            Currency = string.IsNullOrWhiteSpace(currency) ? "NGN" : currency.Trim().ToUpperInvariant();

            if (bankChanged)
            {
                RecipientCode = null;
            }

            UpdateMetadata(modifiedBy);
        }

        public void SetRecipientCode(string recipientCode, string? modifiedBy = null)
        {
            if (string.IsNullOrWhiteSpace(recipientCode))
                throw new ArgumentException("Recipient code is required.", nameof(recipientCode));

            RecipientCode = recipientCode.Trim();
            UpdateMetadata(modifiedBy);
        }

        public void MarkInactive(string? modifiedBy = null)
        {
            IsActive = false;
            UpdateMetadata(modifiedBy);
        }

        private static void ValidateWorkerId(Guid workerId)
        {
            if (workerId == Guid.Empty)
                throw new ArgumentException("WorkerId cannot be empty.", nameof(workerId));
        }

        private static void ValidateBankCode(string bankCode)
        {
            if (string.IsNullOrWhiteSpace(bankCode))
                throw new ArgumentException("Bank code is required.", nameof(bankCode));
        }

        private static void ValidateAccountNumber(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Trim().Length != 10)
                throw new ArgumentException("Account number must be 10 digits.", nameof(accountNumber));
        }

        private static void ValidateAccountName(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
                throw new ArgumentException("Account name is required.", nameof(accountName));
        }
    }
}