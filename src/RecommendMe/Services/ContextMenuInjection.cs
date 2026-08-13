using System;
using System.IO;
using System.Reflection;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Services;

namespace RecommendMe.Services
{
    /// <summary>
    /// RecommendMe context-menu integration using the same mechanism and
    /// structure as the known-working Emby.DataExplorer example.
    /// </summary>
    internal static class ContextMenuInjection
    {
        private const string RecommendScript = @"

                    const recommendMeCommandSource =
                    {
                        getCommands: function getCommands(options) {

                            let commands = [];

                            if (options.items &&
                                options.items.length === 1 &&
                                options.items[0].Id &&
                                recommendMeAllowedItemTypes.indexOf(options.items[0].Type) !== -1) {
                                commands.push({
                                    name: 'RecommendMe',
                                    id: 'recommendme',
                                    icon: 'recommend'
                                });
                                commands.push({
                                    name: 'WatchedStatus',
                                    id: 'watchedstatus',
                                    icon: 'visibility'
                                });
                            }

                            return commands;
                        },

                        executeCommand: function executeCommand(command, items, options) {
                            if (command === 'watchedstatus' && items && items.length === 1) {
                                return require(['components/recommendme/watchedstatus']).then(function(responses) {
                                    const watchedStatus = responses[0].default || responses[0];
                                    return watchedStatus.show(items[0].Id);
                                });
                            }

                            if (command !== 'recommendme' || !items || items.length !== 1) {
                                return Promise.resolve();
                            }

                            const item = items[0];
                            const showToast = function showToast(message) {
                                return Emby.importModule('./modules/toast/toast.js').then(function(toast) {
                                    toast = toast.default || toast;
                                    return toast(message);
                                });
                            };

                            console.log('[RecommendMe] loading eligible targets for item', item.Id);

                            return ApiClient.getJSON(
                                ApiClient.getUrl('RecommendMe/RecommendTargets/' + item.Id))
                            .then(function(targetResult) {
                                if (!targetResult.Allowed || !targetResult.Targets || !targetResult.Targets.length) {
                                    return showToast(
                                        targetResult.Message || 'There are no eligible recipients.');
                                }

                                return require(['modules/actionsheet/actionsheet.js']).then(function(responses) {
                                    const actionSheet = responses[0].default || responses[0];
                                    return actionSheet.show({
                                        title: 'Recommend to...',
                                        items: targetResult.Targets,
                                        positionTo: options && options.positionTo,
                                        resolveWithSelectedItem: true
                                    });
                                }).then(function(target) {
                                    console.log(
                                        '[RecommendMe] recommending item',
                                        item.Id,
                                        'to user',
                                        target.Id);

                                    return ApiClient.ajax({
                                        type: 'POST',
                                        url: ApiClient.getUrl('RecommendMe/Recommend'),
                                        data: JSON.stringify({
                                            ItemId: item.Id,
                                            TargetUserId: target.Id
                                        }),
                                        contentType: 'application/json',
                                        dataType: 'json'
                                    });
                                }).then(function(sendResult) {
                                    return showToast(
                                        sendResult.Message ||
                                        (sendResult.Success
                                            ? 'Recommendation sent.'
                                            : 'Recommendation failed.'));
                                });
                            }).catch(function(error) {
                                // Closing ActionSheet rejects without an Error.
                                if (!error) {
                                    return Promise.resolve();
                                }

                                console.error('[RecommendMe] context-menu command failed', error);
                                return showToast('RecommendMe could not complete that recommendation.');
                            });
                        }
                    }

                    let recommendMeAllowedItemTypes = [];

                    console.log('[RecommendMe] loading context-menu settings');

                    Promise.all([
                        ApiClient.getJSON(ApiClient.getUrl('RecommendMe/ContextMenu/Settings')),
                        Emby.importModule('./modules/common/itemmanager/itemmanager.js')
                    ]).then(function(responses) {
                            recommendMeAllowedItemTypes = responses[0].AllowedItemTypes || [];
                            const itemmanager = responses[1];
                            itemmanager.registerCommandSource(recommendMeCommandSource);
                            console.log(
                                '[RecommendMe] command source registered for item types',
                                recommendMeAllowedItemTypes);
                        }).catch(function(error) {
                            console.error('[RecommendMe] command-source registration failed', error);
                        });

                ";

        private static ILogger logger;

        public static string ModifiedShortcutsString { get; private set; }

        public static MemoryStream WatchedStatusScript { get; private set; }

        public static MemoryStream WatchedStatusTemplate { get; private set; }

        public static void Initialize(
            IServerConfigurationManager configurationManager,
            ILogger pluginLogger)
        {
            logger = pluginLogger;
            var dashboardPath = Path.Combine(
                configurationManager.ApplicationPaths.ApplicationResourcesPath,
                "dashboard-ui");
            if (!string.IsNullOrEmpty(configurationManager.Configuration.DashboardSourcePath))
            {
                dashboardPath = configurationManager.Configuration.DashboardSourcePath;
            }

            var shortcutsPath = Path.Combine(dashboardPath, "modules", "shortcuts.js");
            ModifiedShortcutsString = File.ReadAllText(shortcutsPath) + RecommendScript;
            WatchedStatusScript = GetResourceStream("UI.WatchedStatus.watchedstatus.js");
            WatchedStatusTemplate = GetResourceStream("UI.WatchedStatus.watchedstatus.template.html");
            logger.Info(
                "RecommendMe shortcuts injection initialized from {0}.",
                shortcutsPath);
        }

        private static MemoryStream GetResourceStream(string resourceName)
        {
            var fullName = typeof(ContextMenuInjection).Namespace
                .Replace(".Services", string.Empty) + "." + resourceName;
            var source = typeof(ContextMenuInjection).GetTypeInfo()
                .Assembly.GetManifestResourceStream(fullName);
            if (source == null)
            {
                throw new InvalidOperationException("Missing embedded resource: " + fullName);
            }

            var result = new MemoryStream((int)source.Length);
            source.CopyTo(result);
            return result;
        }

        public static void LogServed(string webPath)
        {
            logger?.Info(
                "RecommendMe served injected shortcuts.js for web path '{0}'.",
                webPath);
        }
    }

    [Route("/{Web}/modules/shortcuts.js", "GET", IsHidden = true)]
    public class GetRecommendMeContextMenuScript
    {
        public string Web { get; set; }
    }

    [Route("/{Web}/components/recommendme/watchedstatus.js", "GET", IsHidden = true)]
    public class GetWatchedStatusScript
    {
        public string Web { get; set; }
    }

    [Route("/{Web}/components/recommendme/watchedstatus.template.html", "GET", IsHidden = true)]
    public class GetWatchedStatusTemplate
    {
        public string Web { get; set; }
    }

    [Unauthenticated]
    public class ContextMenuInjectionService : IService, IRequiresRequest
    {
        private readonly IHttpResultFactory resultFactory;

        public ContextMenuInjectionService(IHttpResultFactory resultFactory)
        {
            this.resultFactory = resultFactory;
        }

        public IRequest Request { get; set; }

        public object Get(GetRecommendMeContextMenuScript request)
        {
            ContextMenuInjection.LogServed(request.Web);
            return this.resultFactory.GetResult(
                ContextMenuInjection.ModifiedShortcutsString.AsSpan(),
                "application/x-javascript");
        }

        public object Get(GetWatchedStatusScript request) =>
            this.resultFactory.GetResult(
                this.Request,
                ContextMenuInjection.WatchedStatusScript.GetBuffer(),
                "application/x-javascript");

        public object Get(GetWatchedStatusTemplate request) =>
            this.resultFactory.GetResult(
                this.Request,
                ContextMenuInjection.WatchedStatusTemplate.GetBuffer(),
                "text/html");
    }
}
