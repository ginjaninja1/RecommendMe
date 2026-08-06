// RecommendMe - Admin-only page security helper
//
// Confirmed via ILSpy (Emby.Web.GenericUI.dll, cross-checked against the
// 4.10.0.22 decompile used to build this plugin):
//   PluginPageControllerHost only calls IPluginPageSecurity.CheckIsUserAuthorised
//   when the page controller implements that interface. EnableInMainMenu /
//   EnableInUserMenu are menu-visibility hints only - there is NO server-side
//   access check without this. Tab controllers are registered independently
//   of their parent controller, so each tab needing admin-only enforcement
//   must implement this itself (see TabPageController's adminOnly flag) -
//   gating only the parent page leaves an admin-only tab reachable by anyone
//   who knows/guesses its pageId.

namespace RecommendMe.UI.Security
{
    using System;
    using System.Threading.Tasks;
    using MediaBrowser.Model.Dto;
    using MediaBrowser.Model.Plugins.UI.Views;

    internal static class AdminOnlyPageSecurity
    {
        public static Task CheckIsAdmin(UserDto user, IPluginUIView requestedView)
        {
            if (user == null || user.Policy == null || !user.Policy.IsAdministrator)
            {
                throw new UnauthorizedAccessException(
                    "This page is available to administrators only.");
            }

            return Task.CompletedTask;
        }
    }
}
