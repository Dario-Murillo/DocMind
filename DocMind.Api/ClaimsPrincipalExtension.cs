namespace DocMind.Api;

using System.Security.Claims;

public static class ClaimsPrincipalExtension
{
    // Identity stores the user's id in the NameIdentifier claim of both the session cookie and the
    // bearer token. Only call this from endpoints with RequireAuthorization(), where it is always set.
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The signed-in user has no NameIdentifier claim.");

        return Guid.Parse(value);
    }
}