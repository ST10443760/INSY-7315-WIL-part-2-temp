using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Models;

namespace ThriveWellness.Controllers;

// MVC controller: the public static pages - home, about and the studio
// policy - plus the framework's generic error view. No service layer
// involved; these pages don't touch the database.
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Policy()
    {
        return View();
    }

    // ASP.NET Core's default unhandled-exception landing page - never cached
    // (an error page for one request shouldn't be served to the next one),
    // and carries the request id so it can be matched up with server logs.
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
