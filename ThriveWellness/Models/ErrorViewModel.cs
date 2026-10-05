namespace ThriveWellness.Models;

// Standard ASP.NET Core MVC scaffolding: shown by the generic Error view,
// carrying the current request's id for support/debugging purposes.
public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
