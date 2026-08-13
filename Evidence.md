## Emby generic UI pages are singleton, server-wide, per pageId — not per-user/session

`UIPagesManager.controllers` is `ConcurrentDictionary<string, PageControllerHostBase>`, keyed
ONLY by pageId. One instance of your `IPluginUIView` serves every user on the server. Anything
stored directly on that view's `ContentData`/fields (search results, form state, etc.) is
visible/overwritten by every other user — confirmed root cause of "user sees another user's
search results" bugs.

Worse: `IPluginUIView.User` (and `IUIView.User`) is ONLY refreshed by the framework on
`GetUIView` (page load / tab switch) — `PageControllerHostBase.RunCommand` receives the real
authenticated `UserDto` from Emby's API layer but never assigns it to `CurrentUIView.User`
before invoking `RunCommand`. So `this.User` inside a page's `RunCommand` is not "who clicked
this" — it's "whoever most recently loaded this page anywhere on the server." Do not use
`this.User` for identity/authorization inside `RunCommand`.

**Working fix pattern** (no adversarial/security requirement — trusted single-tenant use only):
- Add an `OwnerUserId` field to the ContentData/view-model class, `[Browsable(false)]`.
- Stamp it with `value.Id` in the view's `User` property setter (this only fires on
  `GetUIView`, which is trustworthy).
- Because `PageControllerHostBase.RunCommand` deserializes the client's posted `data` string
  fresh on every call (this string is per-request, not shared), and the client always
  round-trips the full ContentData object back on postback, `OwnerUserId` survives the trip.
- In `RunCommand`, deserialize `data` FIRST, pull `OwnerUserId` from it, and resolve the
  calling user (`IUserManager.GetUserById`) from that — never from `this.User`.
- Keep per-user state in a `ConcurrentDictionary<string, TViewModel>` keyed by that same id,
  looked up in both the `User` setter and `RunCommand`.
- `UserDto.Id` is a `string` (not Guid/long) — matches the `GetUserById(string)` overload.
- No eviction is built into this pattern — the dictionary grows for the process lifetime.
  Fine for low user counts; needs `ISessionManager.SessionEnded`-driven cleanup at scale.


 ## Brief for making user-facing RecommendMe pages robust via custom HTML/JS

Why (root cause, confirmed by source inspection):

The current GenericEdit/IPluginUIView page framework has two compounding SDK limitations that make per-user command attribution unreliable:

IPluginUIView.RunCommand has no user parameter, and PageControllerHostBase never refreshes the view's User property before invoking a postback — only on page load.
The dashboard's genericui.js hardcodes every command from every plugin page to a single shared endpoint (POST UI/Command), with no per-button/per-command routing hook — so a plugin cannot redirect specific actions to its own authenticated endpoint while staying inside the GenericEdit framework.

The only way to get genuine per-request authenticated identity is to stop routing state-changing actions through UI/Command and call a plugin-owned API endpoint directly, which requires the client-side code make that call itself — i.e., custom JS, not GenericEdit's generic dispatch.

How (concrete steps for the next implementation pass):

Serve a custom HTML/JS page instead of a GenericEdit-driven IPluginUIView for Recommend and History. Emby plugins can ship their own static HTML/JS via IHasWebPages/GetPages() (needs ILSpy confirmation of the exact registration mechanism/attributes — not yet verified).
Build real IService-derived API endpoints in RecommendMe (Search, Send, Expand, OpenHistory, etc.) with request/response DTOs. Emby's API middleware resolves the real authenticated caller for these independently — confirmed via existing SDK precedent (Emby.Api service classes already use this pattern).
Client JS calls those endpoints via the standard Emby apiClient.ajax (the same helper genericui.js itself uses for UI/Command) — this carries proper per-request auth automatically, same as any other Emby dashboard API call, so identity is no longer something the plugin has to reconstruct or trust from round-tripped state.
Rebuild the current server-rendered layouts as hand-written HTML/JS — this is the real cost. RecommendUI's dropdowns/buttons and HistoryViewBuilder's DxDataGrid are currently free from GenericEdit; a custom page loses that and needs equivalent hand-built markup/JS-driven grid, or a lighter grid library.
The per-user ContentData dictionary work already done doesn't get thrown away — the concept (state keyed by real user id) still applies, it's just populated from genuinely authenticated API calls now instead of GetUIView.

Look-and-feel takeaway from the scan:

The Emby dashboard's UI is not exclusively tied to GenericEdit — its visual components are separable, reusable CSS/JS modules that any custom page can import/reference directly, so a hand-built page doesn't have to look hand-built:

modules/emby-elements/emby-button/emby-button.css — the standard Emby button component/styling.
modules/layout/layout.css and modules/flexstyles.css — shared layout primitives.
modules/themes/*/theme.css — the active theme's color/typography variables (dark, appletv, black, etc.) apply globally regardless of which module renders the markup.
modules/dialoghelper/* — for any modal/dialog needs, matching native Emby dialog chrome.
modules/loading/loading.css + loading.js — the standard loading spinner used by runUiCommand itself.