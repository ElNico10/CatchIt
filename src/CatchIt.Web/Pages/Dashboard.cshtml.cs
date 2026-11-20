using CatchIt.Core.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CatchIt.Web.Pages;

/// <summary>Which watched products have reached their target price (user story #15).</summary>
[Authorize]
public class DashboardModel(DashboardService dashboard) : PageModel
{
    /// <summary>What the dashboard shows.</summary>
    public DashboardView View { get; private set; } = new([], [], 0);

    /// <summary>Loads the dashboard.</summary>
    public void OnGet() => View = dashboard.Load();
}
