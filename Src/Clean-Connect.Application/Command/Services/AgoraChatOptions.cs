namespace Clean_Connect.Application.Command.Services
{
    public class AgoraChatOptions
    {
        public const string SectionName = "AgoraChat";
        public const int MaxTokenTtlSeconds = 24 * 60 * 60;

        public string AppId { get; set; } = string.Empty;
        public string AppKey { get; set; } = string.Empty;
        public string AppCertificate { get; set; } = string.Empty;
        public string ApiHost { get; set; } = string.Empty;
        public int UserTokenTtlSeconds { get; set; } = 30 * 60;
        public int AppTokenTtlSeconds { get; set; } = 30 * 60;
        public bool ProvisionUsers { get; set; } = true;
    }
}
