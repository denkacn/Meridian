namespace MeridianRequestSystem.RequestSystem.Client
{
    public interface IUserClient
    {
        int UserId { get; }
        void SetUserId(int userId);
    }
}
