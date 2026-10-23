using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace SportsClubHub.Web.Components;

/// <summary>
/// Pages that search, filter or edit data use this interactive mode. "prerender: false" means the
/// page loads its data once (when the live connection starts) instead of twice.
/// Pages without it (home, privacy, sign in/out) are plain server-rendered HTML.
/// </summary>
public static class AppRenderModes
{
    public static readonly IComponentRenderMode InteractiveNoPrerender =
        new InteractiveServerRenderMode(prerender: false);
}
