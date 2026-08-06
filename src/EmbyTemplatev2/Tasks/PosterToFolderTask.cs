using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using EmbyTemplatev2.Configuration;
using EmbyTemplatev2.Services;
using EmbyTemplatev2.UI.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmbyTemplatev2.Tasks
{
    /// <summary>
    /// Scheduled task that finds movies and TV shows which have a poster (primary) image
    /// but no folder.ext yet, and copies the poster to folder.ext alongside it.
    /// </summary>
    public class EmbyTemplatev2Task : IScheduledTask
    {
        private readonly ILibraryManager libraryManager;
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;

        public EmbyTemplatev2Task(ILibraryManager libraryManager, IFileSystem fileSystem, ILogManager logManager)
        {
            this.libraryManager = libraryManager;
            this.fileSystem = fileSystem;
            this.logger = logManager.GetLogger("EmbyTemplatev2");
        }

        public string Name => "Copy Posters to Folder Images";

        public string Key => "EmbyTemplatev2Task";

        public string Description => "Finds movies and TV shows with a poster image but no folder image, and copies the poster to folder.ext.";

        public string Category => "GinjaNinja Tools";

        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var options = Plugin.Instance.Configuration;

            if (!options.EnablePlugin)
            {
                this.logger.Info("Poster To Folder is disabled in plugin settings. Exiting without processing.");
                return Task.CompletedTask;
            }

            // Read directly from the Emby-managed configuration object using the correct type
            var filterRows = options.LibraryPaths ?? new List<LibraryPathFilterItem>();

            var enabledPaths = filterRows.Where(p => p.Enabled && !string.IsNullOrEmpty(p.Path)).Select(p => p.Path).ToList();
            var disabledPaths = filterRows.Where(p => !p.Enabled && !string.IsNullOrEmpty(p.Path)).Select(p => p.Path).ToList();

            // Clear, explicit logging based on user configuration state
            if (enabledPaths.Count == 0)
            {
                this.logger.Info("No Libraries/Paths opted in. Exiting without processing.");
                return Task.CompletedTask;
            }

            foreach(var path in enabledPaths)
            {
                this.logger.Info("Library/Paths Opted In: {0}", path);
            }

            // Execute validation specifically using the scoped configuration path rules
            var validationService = new LibraryValidationService(this.libraryManager, this.logger);
            validationService.ValidateActiveConfiguredLibraries(enabledPaths);

            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { typeof(Movie).Name, typeof(Series).Name },
                Recursive = true,
                DtoOptions = new DtoOptions(true),
                IsVirtualItem = false,
            };

            var items = this.libraryManager.GetItemList(query).ToList();
            var copyService = new PosterCopyService(this.logger, this.fileSystem);

            var total = items.Count;
            var processed = 0;

            var copiedCount = 0;
            var skippedCount = 0;
            var erroredCount = 0;

            foreach (var item in items.Where(item => this.IsInScope(item, enabledPaths, disabledPaths)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var typeLabel = item is Series ? "Series" : "Movie";

                var result = copyService.EvaluateAndCopy(item, typeLabel);
                switch (result)
                {
                    case EvaluationResult.Copied:
                        copiedCount++;
                        break;
                    case EvaluationResult.Skipped:
                        skippedCount++;
                        break;
                    case EvaluationResult.Errored:
                        erroredCount++;
                        break;
                }

                processed++;
                progress.Report(total == 0 ? 100.0 : (processed / (double)total) * 100.0);
            }

            // Consolidated summary metric output
            this.logger.Info("{0} Copied, {1} Skipped, {2} Errored", copiedCount, skippedCount, erroredCount);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Determines whether an item's folder falls under an enabled path.
        /// Rules: disabled paths always exclude; the item must fall under an explicitly enabled path.
        /// </summary>
        private bool IsInScope(BaseItem item, List<string> enabledPaths, List<string> disabledPaths)
        {
            var itemPath = item.ContainingFolderPath ?? item.Path;

            if (string.IsNullOrEmpty(itemPath))
            {
                return false;
            }

            if (disabledPaths.Any(p => IsUnderPath(itemPath, p)))
            {
                return false;
            }

            return enabledPaths.Any(p => IsUnderPath(itemPath, p));
        }

        private static bool IsUnderPath(string itemPath, string root)
        {
            if (string.IsNullOrEmpty(root))
            {
                return false;
            }

            var normalizedRoot = root.TrimEnd('\\', '/');
            return itemPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        /*
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerWeekly,
                    DayOfWeek = DayOfWeek.Sunday,
                    TimeOfDayTicks = TimeSpan.FromHours(23).Ticks, // Sunday night
                },
            };
        }
        */
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}
