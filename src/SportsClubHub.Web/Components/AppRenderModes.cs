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
    // Public discovery pages prerender their content and metadata for search engines.
    public static readonly IComponentRenderMode InteractivePrerendered =
        new InteractiveServerRenderMode(prerender: true);

    // Keep this for pages where avoiding a second initialization is more important than crawlable HTML.
    public static readonly IComponentRenderMode InteractiveNoPrerender =
        new InteractiveServerRenderMode(prerender: false);
}
