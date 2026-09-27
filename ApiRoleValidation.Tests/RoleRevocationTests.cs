using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ApiRoleValidation.DTOs;
using ApiRoleValidation.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ApiRoleValidation.Tests;

public class RoleRevocationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RoleRevocationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Admin_Can_Access_Orders_And_Reports()
    {
        var client = _factory.CreateClient();

        // 1. Login as Admin
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Admin123!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Equal(Roles.Admin, auth.Role);

        // 2. Set Bearer token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // 3. Admin can view orders
        var ordersResponse = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.OK, ordersResponse.StatusCode);

        // 4. Admin can view order details
        var orderDetailResponse = await client.GetAsync("/api/orders/1");
        Assert.Equal(HttpStatusCode.OK, orderDetailResponse.StatusCode);

        // 5. Admin can access order reports
        var reportsResponse = await client.GetAsync("/api/reports/orders");
        Assert.Equal(HttpStatusCode.OK, reportsResponse.StatusCode);
        var report = await reportsResponse.Content.ReadFromJsonAsync<OrderReport>();
        Assert.NotNull(report);
        Assert.True(report.TotalOrders > 0);
    }

    [Fact]
    public async Task User_Can_Access_Orders_But_Forbidden_From_Reports()
    {
        var client = _factory.CreateClient();

        // 1. Login as User
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("user1", "User123!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Equal(Roles.User, auth.Role);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // 2. User can view orders
        var ordersResponse = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.OK, ordersResponse.StatusCode);

        // 3. User is forbidden from reports
        var reportsResponse = await client.GetAsync("/api/reports/orders");
        Assert.Equal(HttpStatusCode.Forbidden, reportsResponse.StatusCode);
    }

    [Fact]
    public async Task When_Role_Changed_Token_Is_Immediately_Revoked_And_Refreshed_Token_Has_New_Role()
    {
        var adminClient = _factory.CreateClient();
        var targetUserClient = _factory.CreateClient();

        // 1. Login as Admin
        var adminLogin = await adminClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Admin123!"));
        var adminAuth = await adminLogin.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(adminAuth);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);

        // 2. Find target user 'user1' ID
        var usersResponse = await adminClient.GetFromJsonAsync<List<UserDto>>("/api/auth/users");
        Assert.NotNull(usersResponse);
        var targetUser = usersResponse.First(u => u.Username == "user1");

        // 3. Promote target user to Admin first
        var promoteResponse = await adminClient.PostAsJsonAsync("/api/auth/change-role", new ChangeRoleRequest(targetUser.Id, Roles.Admin));
        Assert.Equal(HttpStatusCode.OK, promoteResponse.StatusCode);

        // 4. Target user logs in as Admin and gets token
        var targetLogin = await targetUserClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("user1", "User123!"));
        var targetAuth = await targetLogin.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(targetAuth);
        Assert.Equal(Roles.Admin, targetAuth.Role);

        targetUserClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", targetAuth.AccessToken);

        // Target user can access admin reports with this token
        var reportAccess = await targetUserClient.GetAsync("/api/reports/orders");
        Assert.Equal(HttpStatusCode.OK, reportAccess.StatusCode);

        // 5. Admin now demotes target user from Admin to User!
        var demoteResponse = await adminClient.PostAsJsonAsync("/api/auth/change-role", new ChangeRoleRequest(targetUser.Id, Roles.User));
        Assert.Equal(HttpStatusCode.OK, demoteResponse.StatusCode);

        // 6. IMMEDIATELY test target user's OLD token:
        // Request to reports endpoint must return 401 Unauthorized (NOT 403, NOT 200) because token was revoked via SecurityStamp!
        var revokedReportsResponse = await targetUserClient.GetAsync("/api/reports/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, revokedReportsResponse.StatusCode);

        // Request to orders endpoint with old token must ALSO return 401 Unauthorized!
        var revokedOrdersResponse = await targetUserClient.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, revokedOrdersResponse.StatusCode);

        // 7. Target user uses their refresh token to obtain a fresh token
        var refreshResponse = await targetUserClient.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest(targetAuth.RefreshToken, targetAuth.AccessToken));
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshedAuth = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshedAuth);
        Assert.Equal(Roles.User, refreshedAuth.Role); // Notice the role is now User!

        // 8. With new token:
        var newClient = _factory.CreateClient();
        newClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshedAuth.AccessToken);

        // Can access orders
        var ordersResponse = await newClient.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.OK, ordersResponse.StatusCode);

        // Forbidden from reports because role is now User
        var reportsResponse = await newClient.GetAsync("/api/reports/orders");
        Assert.Equal(HttpStatusCode.Forbidden, reportsResponse.StatusCode);
    }

    [Fact]
    public async Task SignOut_Immediately_Revokes_Active_Token_And_Refresh_Token()
    {
        var client = _factory.CreateClient();

        // 1. Login as user1
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("user1", "User123!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Verify active token works
        var checkActive = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.OK, checkActive.StatusCode);

        // 2. Sign out
        var signoutResponse = await client.PostAsync("/api/auth/signout", null);
        Assert.Equal(HttpStatusCode.OK, signoutResponse.StatusCode);

        // 3. Immediately try using the same access token
        var checkAfterSignout = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, checkAfterSignout.StatusCode);

        // 4. Try using the refresh token
        var refreshAfterSignout = await client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterSignout.StatusCode);
    }
}
