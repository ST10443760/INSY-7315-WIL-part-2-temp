using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Repositories.Interfaces;

namespace ThriveWellness.Controllers;

[Authorize]
public class ClientsController : Controller
{
    private readonly IClientRepository _clientRepository;

    public ClientsController(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var clients = await _clientRepository.GetAllWithStatsAsync(search);
        ViewData["Search"] = search;
        return View(clients);
    }
}
