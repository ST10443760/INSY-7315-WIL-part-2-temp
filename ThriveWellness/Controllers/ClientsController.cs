using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Authorization;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

// MVC controller: the admin client list, read-only. The list itself is a
// single repository query; the attendance and rewards columns come from
// RewardsService, so they follow the same rules as the client's dashboard.
[Authorize(Policy = AuthPolicies.AdminOnly)]
public class ClientsController : Controller
{
    private readonly IClientRepository _clientRepository;
    private readonly IRewardsService _rewardsService;

    public ClientsController(IClientRepository clientRepository, IRewardsService rewardsService)
    {
        _clientRepository = clientRepository;
        _rewardsService = rewardsService;
    }

    // Every client, optionally filtered by the search box - see
    // ClientRepository.GetAllWithStatsAsync for the actual matching rules -
    // with their classes attended, streak and badges. sort=attended puts the
    // most-attended first; otherwise newest client first, as before.
    public async Task<IActionResult> Index(string? search, string? sort)
    {
        var clients = (await _clientRepository.GetAllWithStatsAsync(search)).ToList();
        var summaries = await _rewardsService.GetSummariesAsync();
        foreach (var client in clients)
        {
            if (summaries.TryGetValue(client.ClientId, out var summary))
            {
                client.AttendedCount = summary.AttendedCount;
                client.CurrentStreak = summary.CurrentStreak;
                client.BadgeCount = summary.BadgeCount;
            }
        }

        var sortByAttended = string.Equals(sort, "attended", StringComparison.OrdinalIgnoreCase);
        if (sortByAttended)
        {
            clients = clients.OrderByDescending(c => c.AttendedCount).ThenBy(c => c.FullName).ToList();
        }

        ViewData["Search"] = search;
        ViewData["SortByAttended"] = sortByAttended;
        return View(clients);
    }
}
