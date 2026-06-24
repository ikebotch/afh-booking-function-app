using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Contracts.V1.Requests.Identity;
using AFH.Booking.Contracts.V1.Responses.Identity;
using AFH.Booking.Function.Functions.V1.Docs;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Booking.Function.Functions.V1.Identity;

[BookingOpenApiTag("Identity")]
public sealed class IdentityAdminFunction
{
    private readonly IBookingIdentityAdminClient _identity;

    public IdentityAdminFunction(IBookingIdentityAdminClient identity)
    {
        _identity = identity;
    }

    [Function("Identity_ListUserProfiles")]
    [BookingOpenApiOperation("Identity", "List identity user profiles", ResponseType = typeof(IReadOnlyList<IdentityUserProfileResponse>))]
    public Task<HttpResponseData> ListUserProfiles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/identity/user-profiles")] HttpRequestData req,
        CancellationToken ct)
        => GetList<IdentityUserProfileResponse>(req, "user-profiles", ct);

    [Function("Identity_GetUserProfile")]
    [BookingOpenApiOperation("Identity", "Get identity user profile", ResponseType = typeof(IdentityUserProfileResponse))]
    public Task<HttpResponseData> GetUserProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/identity/user-profiles/{userProfileId:guid}")] HttpRequestData req,
        Guid userProfileId,
        CancellationToken ct)
        => GetOne<IdentityUserProfileResponse>(req, $"user-profiles/{userProfileId:D}", "User profile was not found.", ct);

    [Function("Identity_UpsertUserProfile")]
    [BookingOpenApiOperation("Identity", "Create or update identity user profile",
        RequestBodyType = typeof(IdentityUserProfileUpsertRequest),
        ResponseType = typeof(IdentityUserProfileResponse))]
    public async Task<HttpResponseData> UpsertUserProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/identity/user-profiles")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityUserProfileUpsertRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Email))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "email is required.", ct, "InvalidUserProfile");

        var result = await _identity.PostAsync<IdentityUserProfileUpsertRequest, IdentityUserProfileResponse>("user-profiles", body, ct);
        return result is null
            ? await DownstreamUnavailable(req, ct)
            : await req.OkJsonAsync(result, ct);
    }

    [Function("Identity_DeleteUserProfile")]
    [BookingOpenApiOperation("Identity", "Delete identity user profile", ResponseType = typeof(object))]
    public Task<HttpResponseData> DeleteUserProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/identity/user-profiles/{userProfileId:guid}")] HttpRequestData req,
        Guid userProfileId,
        CancellationToken ct)
        => Delete(req, $"user-profiles/{userProfileId:D}", "User profile was not found.", ct);

    [Function("Identity_ListPermissions")]
    [BookingOpenApiOperation("Identity", "List permissions", ResponseType = typeof(IReadOnlyList<IdentityPermissionResponse>))]
    public Task<HttpResponseData> ListPermissions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/identity/permissions")] HttpRequestData req,
        CancellationToken ct)
        => GetList<IdentityPermissionResponse>(req, "permissions", ct);

    [Function("Identity_UpsertPermission")]
    [BookingOpenApiOperation("Identity", "Create or update permission",
        RequestBodyType = typeof(IdentityPermissionUpsertRequest),
        ResponseType = typeof(IdentityPermissionResponse))]
    public async Task<HttpResponseData> UpsertPermission(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/identity/permissions")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityPermissionUpsertRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Permission))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "permission is required.", ct, "InvalidPermission");

        var result = await _identity.PostAsync<IdentityPermissionUpsertRequest, IdentityPermissionResponse>("permissions", body, ct);
        return result is null
            ? await DownstreamUnavailable(req, ct)
            : await req.OkJsonAsync(result, ct);
    }

    [Function("Identity_DeletePermission")]
    [BookingOpenApiOperation("Identity", "Delete permission", ResponseType = typeof(object))]
    public Task<HttpResponseData> DeletePermission(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/identity/permissions/{permissionId:guid}")] HttpRequestData req,
        Guid permissionId,
        CancellationToken ct)
        => Delete(req, $"permissions/{permissionId:D}", "Permission was not found.", ct);

    [Function("Identity_ListRoles")]
    [BookingOpenApiOperation("Identity", "List roles", ResponseType = typeof(IReadOnlyList<IdentityRoleResponse>))]
    public Task<HttpResponseData> ListRoles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/identity/roles")] HttpRequestData req,
        CancellationToken ct)
        => GetList<IdentityRoleResponse>(req, "roles", ct);

    [Function("Identity_GetRole")]
    [BookingOpenApiOperation("Identity", "Get role", ResponseType = typeof(IdentityRoleResponse))]
    public Task<HttpResponseData> GetRole(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/identity/roles/{roleId:guid}")] HttpRequestData req,
        Guid roleId,
        CancellationToken ct)
        => GetOne<IdentityRoleResponse>(req, $"roles/{roleId:D}", "Role was not found.", ct);

    [Function("Identity_UpsertRole")]
    [BookingOpenApiOperation("Identity", "Create or update role",
        RequestBodyType = typeof(IdentityRoleUpsertRequest),
        ResponseType = typeof(IdentityRoleResponse))]
    public async Task<HttpResponseData> UpsertRole(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/identity/roles")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityRoleUpsertRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Role))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "role is required.", ct, "InvalidRole");

        var result = await _identity.PostAsync<IdentityRoleUpsertRequest, IdentityRoleResponse>("roles", body, ct);
        return result is null
            ? await DownstreamUnavailable(req, ct)
            : await req.OkJsonAsync(result, ct);
    }

    [Function("Identity_DeleteRole")]
    [BookingOpenApiOperation("Identity", "Delete role", ResponseType = typeof(object))]
    public Task<HttpResponseData> DeleteRole(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/identity/roles/{roleId:guid}")] HttpRequestData req,
        Guid roleId,
        CancellationToken ct)
        => Delete(req, $"roles/{roleId:D}", "Role was not found.", ct);

    [Function("Identity_AddRolePermission")]
    [BookingOpenApiOperation("Identity", "Add permission to role",
        RequestBodyType = typeof(IdentityRolePermissionRequest),
        ResponseType = typeof(IdentityRoleResponse))]
    public async Task<HttpResponseData> AddRolePermission(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/identity/roles/{role}/permissions")] HttpRequestData req,
        string role,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityRolePermissionRequest>(ct);
        if (string.IsNullOrWhiteSpace(role) || body is null || string.IsNullOrWhiteSpace(body.Permission))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "role and permission are required.", ct, "InvalidRolePermission");

        var result = await _identity.PostAsync<IdentityRolePermissionRequest, IdentityRoleResponse>(
            $"roles/{Uri.EscapeDataString(role)}/permissions",
            body,
            ct);
        return result is null
            ? await DownstreamUnavailable(req, ct)
            : await req.OkJsonAsync(result, ct);
    }

    [Function("Identity_RemoveRolePermission")]
    [BookingOpenApiOperation("Identity", "Remove permission from role", ResponseType = typeof(object))]
    public Task<HttpResponseData> RemoveRolePermission(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/identity/roles/{role}/permissions/{permission}")] HttpRequestData req,
        string role,
        string permission,
        CancellationToken ct)
        => Delete(req, $"roles/{Uri.EscapeDataString(role)}/permissions/{Uri.EscapeDataString(permission)}", "Role permission mapping was not found.", ct);

    [Function("Identity_ListUserRoleMappings")]
    [BookingOpenApiOperation("Identity", "List user role mappings", ResponseType = typeof(IReadOnlyList<IdentityUserRoleMappingResponse>))]
    public Task<HttpResponseData> ListUserRoleMappings(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/identity/user-role-mappings")] HttpRequestData req,
        CancellationToken ct)
        => GetList<IdentityUserRoleMappingResponse>(req, "user-role-mappings", ct);

    [Function("Identity_AssignUserRole")]
    [BookingOpenApiOperation("Identity", "Assign user role",
        RequestBodyType = typeof(IdentityUserRoleMappingRequest),
        ResponseType = typeof(IdentityUserRoleMappingResponse))]
    public async Task<HttpResponseData> AssignUserRole(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/identity/user-role-mappings")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityUserRoleMappingRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Role))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "role is required.", ct, "InvalidUserRoleMapping");

        var result = await _identity.PostAsync<IdentityUserRoleMappingRequest, IdentityUserRoleMappingResponse>("user-role-mappings", body, ct);
        return result is null
            ? await DownstreamUnavailable(req, ct)
            : await req.CreatedJsonAsync(result, ct);
    }

    [Function("Identity_DeleteUserRoleMapping")]
    [BookingOpenApiOperation("Identity", "Delete user role mapping", ResponseType = typeof(object))]
    public Task<HttpResponseData> DeleteUserRoleMapping(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/identity/user-role-mappings/{mappingId:guid}")] HttpRequestData req,
        Guid mappingId,
        CancellationToken ct)
        => Delete(req, $"user-role-mappings/{mappingId:D}", "User role mapping was not found.", ct);

    [Function("Identity_ListUserPermissionMappings")]
    [BookingOpenApiOperation("Identity", "List user permission mappings", ResponseType = typeof(IReadOnlyList<IdentityUserPermissionMappingResponse>))]
    public Task<HttpResponseData> ListUserPermissionMappings(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/identity/user-permission-mappings")] HttpRequestData req,
        CancellationToken ct)
        => GetList<IdentityUserPermissionMappingResponse>(req, "user-permission-mappings", ct);

    [Function("Identity_AssignUserPermission")]
    [BookingOpenApiOperation("Identity", "Grant or deny user permission",
        RequestBodyType = typeof(IdentityUserPermissionMappingRequest),
        ResponseType = typeof(IdentityUserPermissionMappingResponse))]
    public async Task<HttpResponseData> AssignUserPermission(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/identity/user-permission-mappings")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityUserPermissionMappingRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Permission))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "permission is required.", ct, "InvalidUserPermissionMapping");

        var result = await _identity.PostAsync<IdentityUserPermissionMappingRequest, IdentityUserPermissionMappingResponse>("user-permission-mappings", body, ct);
        return result is null
            ? await DownstreamUnavailable(req, ct)
            : await req.CreatedJsonAsync(result, ct);
    }

    [Function("Identity_DeleteUserPermissionMapping")]
    [BookingOpenApiOperation("Identity", "Delete user permission mapping", ResponseType = typeof(object))]
    public Task<HttpResponseData> DeleteUserPermissionMapping(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/identity/user-permission-mappings/{mappingId:guid}")] HttpRequestData req,
        Guid mappingId,
        CancellationToken ct)
        => Delete(req, $"user-permission-mappings/{mappingId:D}", "User permission mapping was not found.", ct);

    private async Task<HttpResponseData> GetList<T>(HttpRequestData req, string path, CancellationToken ct)
    {
        var result = await _identity.GetAsync<IReadOnlyList<T>>(path, ct);
        return result is null
            ? await DownstreamUnavailable(req, ct)
            : await req.OkJsonAsync(result, ct, HttpResponseExtensions.SinglePage(result.Count));
    }

    private async Task<HttpResponseData> GetOne<T>(HttpRequestData req, string path, string notFoundMessage, CancellationToken ct)
    {
        var result = await _identity.GetAsync<T>(path, ct);
        return result is null
            ? await req.ProblemAsync(HttpStatusCode.NotFound, notFoundMessage, ct, "NotFound")
            : await req.OkJsonAsync(result, ct);
    }

    private async Task<HttpResponseData> Delete(HttpRequestData req, string path, string notFoundMessage, CancellationToken ct)
    {
        var deleted = await _identity.DeleteAsync(path, ct);
        return deleted
            ? await req.OkJsonAsync(new { deleted = true }, ct)
            : await req.ProblemAsync(HttpStatusCode.NotFound, notFoundMessage, ct, "NotFound");
    }

    private static Task<HttpResponseData> DownstreamUnavailable(HttpRequestData req, CancellationToken ct) =>
        req.ProblemAsync(HttpStatusCode.BadGateway, "Location Identity service did not return a successful response.", ct, "IdentityUnavailable");
}
