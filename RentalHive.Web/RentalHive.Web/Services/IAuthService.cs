namespace RentalHive.Web.Services
{
    public interface IAuthService
    {
        Task LoginAsync(string token);
        Task LogoutAsync();
    }
}
