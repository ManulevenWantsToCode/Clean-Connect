using Clean_Connect.Application.DTO;

namespace Clean_Connect.Application.Interface.Services
{
    public interface IAgoraChatTokenService
    {
        AgoraChatTokenResult CreateUserToken(string userId);
        string CreateAppToken();
    }
}
