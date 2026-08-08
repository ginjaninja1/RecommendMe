using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace RecommendMe.Storage
{
    /// <summary>Serializes access to a single JSON data file and replaces it safely.</summary>
    internal class JsonFileRepository<T>
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
            this.FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                this.WriteUnlocked(value);
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
                    return this.jsonSerializer.DeserializeFromStream<T>(stream)
                        ?? throw new InvalidDataException($"Data file '{this.FilePath}' contained no JSON value.");
                }
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("Error reading data file {0}", ex, this.FilePath);
                throw new InvalidDataException(
                    $"RecommendMe could not read '{this.FilePath}'. The existing file was left unchanged.",
                    ex);
            }
        }

        private void WriteUnlocked(T value)
        {
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

            if (!this.fileSystem.FileExists(this.FilePath))
            {
                this.fileSystem.MoveFile(tempPath, this.FilePath);
                return;
            }

            var backupPath = this.FilePath + ".bak";
            this.fileSystem.CopyFile(this.FilePath, backupPath, true);
            this.fileSystem.SwapFiles(this.FilePath, tempPath);
            this.fileSystem.DeleteFile(tempPath);
        }
    }
}
