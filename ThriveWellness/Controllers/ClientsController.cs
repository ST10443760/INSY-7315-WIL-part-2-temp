using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Repositories.Interfaces;

namespace ThriveWellness.Controllers;

// MVC controller: the admin client list, read-only - there's no separate
// client-management service, since this is a single query with no business
// rules attached, so the controller talks to the repository directly.
[Authorize]
public class ClientsController : Controller
{
    private readonly IClientRepository _clientRepository;

    public ClientsController(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    // Every client, optionally filtered by the search box - see
    // ClientRepository.GetAllWithStatsAsync for the actual matching rules.
    public async Task<IActionResult> Index(string? search)
    {
        var clients = await _clientRepository.GetAllWithStatsAsync(search);
        ViewData["Search"] = search;
        return View(clients);
    }
}
