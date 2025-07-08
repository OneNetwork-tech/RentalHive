using Microsoft.JSInterop;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using RentalHive.Web.Auth;
using System.Security.Claims;
// using System.Collections.Generic;

namespace RentalHive.Web.Services
{
    public class AuthService : IAuthService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly CustomAuthenticationStateProvider _authenticationStateProvider;

        public AuthService(IJSRuntime jsRuntime, CustomAuthenticationStateProvider authenticationStateProvider)
        {
            _jsRuntime = jsRuntime;
            _authenticationStateProvider = authenticationStateProvider;
        }

        public async Task LoginAsync(string token)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);
            _authenticationStateProvider.NotifyUserAuthentication(token);
        }

        public async Task LogoutAsync()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
            _authenticationStateProvider.NotifyUserLogout();
        }
    }
}
