using KirkukInstitute.Ai;
using KirkukInstitute.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KirkukInstitute.Controllers;

/// <summary>Chat page for the read-only AI assistant. All answers come from <see cref="AiController"/> (/api/ai/query).</summary>
[Authorize(Roles = Roles.Staff)]
public class AssistantController : Controller
{
    private readonly AiToolRegistry _registry;

    public AssistantController(AiToolRegistry registry) => _registry = registry;

    public IActionResult Index()
    {
        var caller = AssistantService.CallerFromPrincipal(User, "ar");
        ViewBag.ToolCount = _registry.ForCaller(caller).Count;
        ViewBag.CanSeeFinance = caller.CanSeeFinance;
        return View();
    }
}
