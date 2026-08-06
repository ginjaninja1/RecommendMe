using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace RecommendMe.Storage
{
    /// <summary>
    /// Generic async-safe JSON read/write helper for a single file. Every
    /// concrete store (<see cref="AdminSettingsStore"/>, <see cref="RecommendationStore"/>,
    /// <see cref="UserPreferenceStore"/>, <see cref="CollectionRegistryStore"/>) wraps one
    /// of these rather than talking to disk directly.
    ///
    /// Concurrency model: a single process-wide <see cref="SemaphoreSlim"/> per
    /// file serializes all reads and writes to that file. Emby plugins run
    /// in-process and single-instance, so file-level (not just in-memory)
    /// locking is sufficient here - there is no multi-process writer to guard
    /// against, only concurrent async requests within this plugin.
    /// </summary>
    public class JsonFileRepository<T>
        where T : class, new()
    {
        private readonly IFileSystem fileSystem;
        private readonly IJsonSerializer jsonSerializer;
        private readonly ILogger logger;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        public JsonFileRepository(
            string filePath,
            IFileSystem fileSystem,
            IJsonSerializer jsonSerializer,
            ILogger logger)
        {
            this.FilePath = filePath;
            this.fileSystem = fileSystem;
            this.jsonSerializer = jsonSerializer;
            this.logger = logger;
        }

        public string FilePath { get; }

        public async Task<T> ReadAsync()
        {
            await this.gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return this.ReadUnlocked();
            }
            finally
            {
                this.gate.Release();
            }
        }

        public async Task WriteAsync(T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            await this.gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var directory = Path.GetDirectoryName(this.FilePath);
                if (!string.IsNullOrEmpty(directory) && !this.fileSystem.DirectoryExists(directory))
                {
                    this.fileSystem.CreateDirectory(directory);
                }

                // Write to a temp file then swap in, so a crash mid-write can
                // never leave a half-written / corrupt JSON file on disk.
                var tempPath = this.FilePath + ".tmp";

                using (var stream = this.fileSystem.GetFileStream(tempPath, FileOpenMode.Create, FileAccessMode.Write))
                {
                    this.jsonSerializer.SerializeToStream(value, stream);
                }

                if (this.fileSystem.FileExists(this.FilePath))
                {
                    this.fileSystem.DeleteFile(this.FilePath);
                }

                this.fileSystem.MoveFile(tempPath, this.FilePath);
            }
            finally
            {
                this.gate.Release();
            }
        }

        /// <summary>
        /// Read-modify-write under a single lock hold, so callers doing
        /// "load, mutate, save" don't race with another caller doing the same.
        /// </summary>
        public async Task MutateAsync(Action<T> mutate)
        {
            if (mutate == null)
            {
                throw new ArgumentNullException(nameof(mutate));
            }

            await this.gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var value = this.ReadUnlocked();
                mutate(value);

                var directory = Path.GetDirectoryName(this.FilePath);
                if (!string.IsNullOrEmpty(directory) && !this.fileSystem.DirectoryExists(directory))
                {
                    this.fileSystem.CreateDirectory(directory);
                }

                var tempPath = this.FilePath + ".tmp";

                using (var stream = this.fileSystem.GetFileStream(tempPath, FileOpenMode.Create, FileAccessMode.Write))
                {
                    this.jsonSerializer.SerializeToStream(value, stream);
                }

                if (this.fileSystem.FileExists(this.FilePath))
                {
                    this.fileSystem.DeleteFile(this.FilePath);
                }

                this.fileSystem.MoveFile(tempPath, this.FilePath);
            }
            finally
            {
                this.gate.Release();
            }
        }

        private T ReadUnlocked()
        {
            if (!this.fileSystem.FileExists(this.FilePath))
            {
                return new T();
            }

            try
            {
                using (var stream = this.fileSystem.OpenRead(this.FilePath))
                {
                    var value = this.jsonSerializer.DeserializeFromStream<T>(stream);
                    return value ?? new T();
                }
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("Error reading RecommendMe data file {0}", ex, this.FilePath);
                return new T();
            }
        }
    }
}
