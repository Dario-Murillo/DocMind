namespace DocMind.Core.Users;

using Microsoft.AspNetCore.Identity;

// Guid keys (instead of Identity's default string) so the user_id foreign keys added in later
// phases on documents, chunks and conversations are native uuid columns. Kept as our own type,
// even while empty, so profile fields can be added later without touching every IdentityUser
// reference.
public class ApplicationUser : IdentityUser<Guid>
{
}