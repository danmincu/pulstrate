namespace TaskServer.Infrastructure.Authentication;

public class FirebaseAuthenticationOptions
{
    public const string SectionName = "Authentication:Firebase";

    public string ProjectId { get; set; } = "qdrix-12345";
    public string ValidAudience { get; set; } = "qdrix-12345";
    public string ValidIssuer { get; set; } = "https://securetoken.google.com/qdrix-12345";
}
