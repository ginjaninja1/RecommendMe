Below is the complete structure needed to add Emby web-client context-menu commands, using the two working patterns.
1. Common context-menu infrastructure
Both implementations work by intercepting Emby’s web-client module:
/{Web}/modules/shortcuts.js
The plugin:
Reads Emby’s original dashboard-ui/modules/shortcuts.js.
Appends custom JavaScript.
Registers an HTTP route that serves the combined script.
The appended JavaScript registers a command source with Emby’s itemmanager.
Plugin initialization
RecommendMe initializes the injection from its plugin constructor:
ContextMenuInjection.Initialize(
    applicationHost.Resolve<IServerConfigurationManager>(),
    this.logger);
See [Plugin.cs (line 54)](C:/Development/RecommendMe/src/RecommendMe/Plugin.cs:54).
This must happen while the plugin is being constructed, before the web client requests shortcuts.js.
Reading and modifying shortcuts.js
The central class is [ContextMenuInjection.cs (line 14)](C:/Development/RecommendMe/src/RecommendMe/Services/ContextMenuInjection.cs:14).
Its initialization method finds Emby’s current dashboard directory:
var dashboardPath = Path.Combine(
    configurationManager.ApplicationPaths.ApplicationResourcesPath,
    "dashboard-ui");

if (!string.IsNullOrEmpty(
        configurationManager.Configuration.DashboardSourcePath))
{
    dashboardPath =
        configurationManager.Configuration.DashboardSourcePath;
}

var shortcutsPath =
    Path.Combine(dashboardPath, "modules", "shortcuts.js");

ModifiedShortcutsString =
    File.ReadAllText(shortcutsPath) + RecommendScript;
The important point is that the original Emby script is preserved. The plugin serves:
original shortcuts.js + custom command JavaScript
Route DTO
The route DTO claims the same URL normally used for Emby’s module:
[Route("/{Web}/modules/shortcuts.js", "GET", IsHidden = true)]
public class GetRecommendMeContextMenuScript
{
    public string Web { get; set; }
}
{Web} normally resolves to web, including when the request is made through Emby’s /emby API prefix.
Route service
The service returns the modified JavaScript:
[Unauthenticated]
public class ContextMenuInjectionService : IService, IRequiresRequest
{
    private readonly IHttpResultFactory resultFactory;

    public ContextMenuInjectionService(
        IHttpResultFactory resultFactory)
    {
        this.resultFactory = resultFactory;
    }

    public IRequest Request { get; set; }

    public object Get(
        GetRecommendMeContextMenuScript request)
    {
        return this.resultFactory.GetResult(
            ContextMenuInjection.ModifiedShortcutsString.AsSpan(),
            "application/x-javascript");
    }
}
This route must be [Unauthenticated] because Emby may request core web-client JavaScript before an authenticated user session exists.
2. The Emby command-source contract
The injected JavaScript supplies an object with two methods:
const commandSource = {
    getCommands: function (options) {
        // Return commands that should appear.
    },

    executeCommand: function (command, items, options) {
        // Execute the selected command.
    }
};
It is registered with:
Emby.importModule(
    './modules/common/itemmanager/itemmanager.js'
).then(function (itemmanager) {
    itemmanager.registerCommandSource(commandSource);
});
getCommands(options)
Important properties include:
options.items
options.items[0].Id
options.items[0].Type
For a single-item command:
getCommands: function getCommands(options) {
    let commands = [];

    if (options.items &&
        options.items.length === 1 &&
        options.items[0].Id) {

        commands.push({
            name: 'My command',
            id: 'my-command',
            icon: 'recommend'
        });
    }

    return commands;
}
The returned object uses:
name: visible menu label.
id: identifier passed to executeCommand.
icon: Emby Material icon name.
executeCommand(command, items, options)
Always ensure that the command belongs to your source:
executeCommand: function executeCommand(
    command,
    items,
    options) {

    if (command !== 'my-command' ||
        !items ||
        items.length !== 1) {
        return Promise.resolve();
    }

    const selectedItem = items[0];
}
Return a promise so Emby can track command completion and close/resume its menu correctly.
Example A: server-generated user picker
RecommendMe uses a second ActionSheet after the user selects RecommendMe.
This is a sequential cascade:
Context menu → RecommendMe → user ActionSheet
It is not a native hover submenu. Emby’s command-source API only returns a flat list.
Client-side media-type filtering
RecommendMe first loads the administrator-enabled item types:
let recommendMeAllowedItemTypes = [];

Promise.all([
    ApiClient.getJSON(
        ApiClient.getUrl(
            'RecommendMe/ContextMenu/Settings')),
    Emby.importModule(
        './modules/common/itemmanager/itemmanager.js')
]).then(function (responses) {
    recommendMeAllowedItemTypes =
        responses[0].AllowedItemTypes || [];

    const itemmanager = responses[1];
    itemmanager.registerCommandSource(
        recommendMeCommandSource);
});
The context-menu command is only returned for those types:
getCommands: function getCommands(options) {
    let commands = [];

    if (options.items &&
        options.items.length === 1 &&
        options.items[0].Id &&
        recommendMeAllowedItemTypes.indexOf(
            options.items[0].Type) !== -1) {

        commands.push({
            name: 'RecommendMe',
            id: 'recommendme',
            icon: 'recommend'
        });
    }

    return commands;
}
This is only a presentation filter. The server still validates the type later.
Loading the target users
When RecommendMe is selected:
return ApiClient.getJSON(
    ApiClient.getUrl(
        'RecommendMe/RecommendTargets/' + item.Id)
).then(function (targetResult) {
    // Open ActionSheet.
});
The server response looks like:
{
  "Allowed": true,
  "Message": null,
  "Targets": [
    {
      "Id": "1",
      "Name": "Cartman (yourself)"
    },
    {
      "Id": "4",
      "Name": "Test"
    }
  ]
}
Opening ActionSheet
The users are passed to Emby’s ActionSheet:
return require([
    'modules/actionsheet/actionsheet.js'
]).then(function (responses) {
    const actionSheet =
        responses[0].default || responses[0];

    return actionSheet.show({
        title: 'Recommend to...',
        items: targetResult.Targets,
        positionTo: options && options.positionTo,
        resolveWithSelectedItem: true
    });
});
The default || direct normalization matters because different Emby modules/builds may wrap the exported object.
Each picker item requires at least:
{
    Id: '4',
    Name: 'Test'
}
With resolveWithSelectedItem: true, ActionSheet resolves with the complete selected object:
.then(function (target) {
    console.log(target.Id);
    console.log(target.Name);
});
Without that option, it normally resolves with only the item ID.
Posting the selection
RecommendMe sends only the selected media item and recipient:
return ApiClient.ajax({
    type: 'POST',
    url: ApiClient.getUrl(
        'RecommendMe/Recommend'),
    data: JSON.stringify({
        ItemId: item.Id,
        TargetUserId: target.Id
    }),
    contentType: 'application/json',
    dataType: 'json'
});
There is deliberately no sender ID. The server derives the sender from the authenticated request.
Server DTOs
The DTOs are in [ContextMenuDtos.cs](C:/Development/RecommendMe/src/RecommendMe/Services/ContextMenuDtos.cs).
Allowed types
[Route("/RecommendMe/ContextMenu/Settings", "GET")]
public class GetRecommendContextMenuSettings
{
}
Response:
public class RecommendContextMenuSettings
{
    public List<string> AllowedItemTypes { get; set; }
}
Target users
[Route("/RecommendMe/RecommendTargets/{ItemId}", "GET")]
public class GetRecommendTargets
{
    public long ItemId { get; set; }
}
Response items:
public class RecommendTargetDto
{
    public string Id { get; set; }

    public string Name { get; set; }
}
Sending
[Route("/RecommendMe/Recommend", "POST")]
public class SendContextMenuRecommendation
{
    public long ItemId { get; set; }

    public long TargetUserId { get; set; }
}
Authenticated API service
[ContextMenuApiService.cs](C:/Development/RecommendMe/src/RecommendMe/Services/ContextMenuApiService.cs) implements all three operations:
[Authenticated]
public class ContextMenuApiService :
    IService,
    IRequiresRequest
It injects:
IAuthorizationContext authorizationContext
and gets the caller from the request:
var authorization =
    authorizationContext.GetAuthorizationInfo(Request);

var sender = Plugin.Instance.GetAllUsers()
    .FirstOrDefault(user =>
        user.InternalId == authorization.UserId);
The client cannot spoof the sender.
Target-generation flow
Get(GetRecommendTargets request):
Resolves the authenticated sender.
Loads the selected BaseItem.
Checks that the sender can see the item.
Checks that the item type is enabled.
Checks whether the sender is suspended.
Evaluates every possible recipient with PermissionService.CanSendAsync.
Removes recipients who cannot see the item.
Returns the eligible users.
The central plugin permission method is:
PermissionService.CanSendAsync(
    sender,
    candidate,
    item.GetType().Name)
That applies:
Global allowed media types.
Sender and recipient suspension.
Everyone/No One/Allowed Users/Group Members policies.
Recipient blocks.
Recipient media-type opt-outs.
Final send flow
Post(SendContextMenuRecommendation request) repeats validation and then calls:
RecommendationService.SendRecommendationAsync(
    sender,
    recipient,
    item,
    item.GetType().Name,
    false);
That additionally validates:
Emby item visibility.
Blocked tags and parental restrictions.
Library access.
Watched state.
Existing recommendation-collection membership.
The target picker is therefore a convenience filter, while the POST endpoint remains the security boundary.
Example B: launch a custom dialog
DataExplorer uses the same command registration, but executeCommand imports a plugin-owned JavaScript component:
executeCommand: function executeCommand(
    command,
    items,
    options) {

    return require([
        'components/dataexplorer/dataexplorer'
    ]).then(function (responses) {
        return responses[0].show(items[0].Id);
    });
}
Additional component routes
DataExplorer registers routes for its JavaScript and HTML template:
[Route(
    "/{Web}/components/dataexplorer/dataexplorer.js",
    "GET",
    IsHidden = true)]
[Unauthenticated]
public class GetDataExplorerJs
{
    public string Web { get; set; }
}
[Route(
    "/{Web}/components/dataexplorer/dataexplorer.template.html",
    "GET",
    IsHidden = true)]
[Unauthenticated]
public class GetDataExplorerHtml
{
    public string Web { get; set; }
}
Its injection service returns the embedded resources:
public object Get(GetDataExplorerJs request)
{
    return resultFactory.GetResult(
        Request,
        ContextMenuHelper.ViewerScript.GetBuffer(),
        "application/x-javascript");
}

public object Get(GetDataExplorerHtml request)
{
    return resultFactory.GetResult(
        Request,
        ContextMenuHelper.ViewerTemplate.GetBuffer(),
        "text/html");
}
JavaScript component
The component is an AMD module:
define([
    'connectionManager',
    'dialogHelper',
    'globalize',
    'loading',
    'formDialogStyle'
], function (
    connectionManager,
    dialogHelper,
    globalize,
    loading) {

    return {
        show: function (itemId) {
            // Create and open dialog.
        }
    };
});
Its show(itemId) method:
Loads the HTML template.
Creates a dialog with dialogHelper.createDialog.
Places the template into the dialog.
Opens it with dialogHelper.open.
Loads item data through ApiClient.
Resolves or rejects when the dialog closes.
A simplified version is:
show: function (itemId) {
    return new Promise(function (resolve, reject) {
        const xhr = new XMLHttpRequest();

        xhr.open(
            'GET',
            'components/myplugin/dialog.template.html',
            true);

        xhr.onload = function () {
            const dialogOptions = {
                removeOnClose: true,
                size: 'fullscreen-border',
                scrollY: true,
                autoFocus: false
            };

            const dlg =
                dialogHelper.createDialog(dialogOptions);

            dlg.classList.add('formDialog');
            dlg.innerHTML =
                globalize.translateDocument(this.response);

            dialogHelper.open(dlg);

            dlg.addEventListener('close', function () {
                if (dlg.submitted) {
                    resolve();
                } else {
                    reject();
                }
            });

            // Load and render plugin data here.
        };

        xhr.send();
    });
}
The JavaScript and template must be embedded in the plugin assembly, normally through the project file:
<ItemGroup>
  <EmbeddedResource
      Include="Web\mydialog.js"
      LogicalName="MyPlugin.Web.mydialog.js" />
  <EmbeddedResource
      Include="Web\mydialog.template.html"
      LogicalName="MyPlugin.Web.mydialog.template.html" />
</ItemGroup>
Key difference between the patterns
Pattern	executeCommand action	Additional resources
DataExplorer dialog	Imports a plugin component and calls show(itemId)	Component JS, HTML template, routes serving both
RecommendMe picker	Calls a plugin API, then opens Emby ActionSheet	Authenticated settings, targets, and send endpoints
Simple command	Directly performs an action or shows a toast	Usually none


Operational requirements
For a changed plugin DLL:
Copy it into Emby’s plugin directory.
Restart Emby so the new assembly and routes are loaded.
Load the web client hosted by that server.
Hard refresh or reopen the tab so shortcuts.js is fetched again.
A browser restart and F12 are not normally required. F12 is useful only for diagnosing JavaScript errors.
One architectural limitation is important: only one plugin can reliably claim the exact /{Web}/modules/shortcuts.js route. Two plugins using this technique may conflict depending on which route Emby registers.