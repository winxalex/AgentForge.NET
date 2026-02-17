using Microsoft.Extensions.Hosting; // For IHostEnvironment
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace Chat2Report.Providers
{
    public class FilePromptProvider : IPromptProvider, IDisposable
    {
        private readonly IHostEnvironment _environment;
        private readonly ILogger<FilePromptProvider> _logger;
        private readonly ConcurrentDictionary<string, string> _promptCache = new(); // Key: Absolute Path, Value: Content
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new(); // Key: Directory Path
        private readonly ConcurrentDictionary<string, byte> _watchedFiles = new(); // Key: Absolute File Path (using byte as a dummy value for set-like behavior)
        private const string FilePrefix = "file:";
        private bool _disposed = false;


        public FilePromptProvider(IHostEnvironment environment, ILogger<FilePromptProvider> logger)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("FilePromptProvider initialized. Watching for file changes to invalidate cache.");
        }

        public async Task<string> GetPromptAsync(string promptIdentifier)
        {
            if (string.IsNullOrWhiteSpace(promptIdentifier))
            {
                return string.Empty;
            }

            if (!promptIdentifier.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Not a file identifier, return as is
                return promptIdentifier;
            }

            string relativePath = promptIdentifier.Substring(FilePrefix.Length);
            string absolutePath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, relativePath)); // Use GetFullPath for robustness

            // 1. Check Cache First
            if (_promptCache.TryGetValue(absolutePath, out var cachedContent))
            {
                _logger.LogTrace("Cache hit for prompt file: {AbsolutePath}", absolutePath);
                return cachedContent;
            }

            // 2. Cache Miss: Read from file system
            _logger.LogDebug("Cache miss. Reading prompt file: {AbsolutePath}", absolutePath);
            try
            {
                if (!File.Exists(absolutePath))
                {
                    _logger.LogWarning("Prompt file not found: {AbsolutePath}. Returning original identifier.", absolutePath);
                    // Decide what to return: original identifier, empty, throw?
                    // Returning original allows Agent config to be the fallback, but might hide errors.
                    // Returning "" might be safer if agent handles empty prompts. Let's return "" for now.
                    return ""; // Or return promptIdentifier; or throw new FileNotFoundException(...);
                }

                string content = await File.ReadAllTextAsync(absolutePath);

                // 3. Update Cache
                _promptCache[absolutePath] = content;
                _logger.LogDebug("Cached prompt content for: {AbsolutePath}", absolutePath);

                // 4. Ensure File is Watched (do this *after* successful read)
                EnsureWatching(absolutePath);

                return content;
            }
            catch (IOException ioEx) // Catch specific IO errors
            {
                 _logger.LogError(ioEx, "IO Error reading prompt file: {AbsolutePath}. Check file access permissions.", absolutePath);
                 // Decide on fallback. Maybe return empty string or throw a specific exception.
                 return ""; // Or throw new InvalidOperationException($"Failed to read prompt file '{absolutePath}'.", ioEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error reading prompt file: {AbsolutePath}", absolutePath);
                // Decide on fallback
                return ""; // Or throw;
            }
        }

        private void EnsureWatching(string absoluteFilePath)
        {
            if (_watchedFiles.ContainsKey(absoluteFilePath))
            {
                // Already watching this specific file (implicitly via its directory watcher)
                return;
            }

            string directory = Path.GetDirectoryName(absoluteFilePath);
            if (directory == null)
            {
                 _logger.LogWarning("Could not determine directory for prompt file: {absoluteFilePath}. Cannot watch for changes.", absoluteFilePath);
                 return;
            }

            // Use GetOrAdd for thread-safe watcher creation per directory
            var watcher = _watchers.GetOrAdd(directory, dir => {
                _logger.LogInformation("Creating FileSystemWatcher for directory: {Directory}", dir);
                var newWatcher = new FileSystemWatcher(dir)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName, // Watch for changes, renames, deletes
                    IncludeSubdirectories = false, // Typically prompts are not in subdirs
                    EnableRaisingEvents = true, // Start watching
                };

                // Use lambda expressions to capture the logger
                 newWatcher.Changed += (sender, e) => OnFileChanged(sender, e);
                 newWatcher.Deleted += (sender, e) => OnFileDeleted(sender, e);
                 newWatcher.Renamed += (sender, e) => OnFileRenamed(sender, e);
                 newWatcher.Error += (sender, e) => OnWatcherError(sender, e);


                return newWatcher;
            });

             // Mark this specific file as being monitored
             _watchedFiles.TryAdd(absoluteFilePath, 0);
              _logger.LogDebug("Now actively monitoring prompt file: {absoluteFilePath} via watcher on {directory}", absoluteFilePath, directory);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            _logger.LogInformation("Detected change in watched file: {FullPath}. Invalidating cache.", e.FullPath);
            // Use TryRemove, it's okay if the key isn't present (e.g., file change before first read)
             if (_promptCache.TryRemove(e.FullPath, out _))
             {
                  _logger.LogDebug("Removed changed file from prompt cache: {FullPath}", e.FullPath);
             }
             // Also check if it's a directory change that might affect our watcher base path (less common)
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            _logger.LogInformation("Detected deletion of watched file: {FullPath}. Invalidating cache.", e.FullPath);
            if (_promptCache.TryRemove(e.FullPath, out _))
            {
                 _logger.LogDebug("Removed deleted file from prompt cache: {FullPath}", e.FullPath);
            }
             // Also remove from the set of specifically watched files
             _watchedFiles.TryRemove(e.FullPath, out _);
             // We could potentially dispose the watcher if no more files in that dir are watched, but adds complexity.
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            _logger.LogInformation("Detected rename affecting watched file: {OldFullPath} -> {FullPath}. Invalidating cache for old path.", e.OldFullPath, e.FullPath);
            // Remove the old path from cache and watched set
             if (_promptCache.TryRemove(e.OldFullPath, out _))
             {
                 _logger.LogDebug("Removed renamed (old) file from prompt cache: {OldFullPath}", e.OldFullPath);
             }
             _watchedFiles.TryRemove(e.OldFullPath, out _);

             // If the *new* path is one we care about (i.e. it matches a `file:` reference in config
             // that might be requested later), we might want to proactively cache it or ensure it's watched.
             // However, the current logic handles this lazily: the next GetPromptAsync for the *new* path
             // will trigger a read and ensure watching. This is usually sufficient.
        }

         private void OnWatcherError(object sender, ErrorEventArgs e)
         {
             // Log errors from the FileSystemWatcher itself
             Exception ex = e.GetException();
             _logger.LogError(ex, "FileSystemWatcher encountered an error. Prompt reloading may be affected.");
             // Depending on the error, might need to recreate the watcher.
             // For simplicity now, we just log it. Common errors include buffer overflows
             // if many changes happen quickly, or access denied issues.
         }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _logger.LogInformation("Disposing FilePromptProvider. Stopping file watchers.");
                // Dispose managed state (managed objects).
                foreach (var kvp in _watchers)
                {
                    kvp.Value.EnableRaisingEvents = false; // Stop events first
                    kvp.Value.Dispose();
                    _logger.LogDebug("Disposed watcher for directory: {Directory}", kvp.Key);
                }
                _watchers.Clear();
                _promptCache.Clear();
                 _watchedFiles.Clear();
            }

            // Free unmanaged resources (unmanaged objects) and override finalizer
            // Not needed here as FileSystemWatcher handles its resources via Dispose

            _disposed = true;
        }

        // Optional Finalizer (needed only if you have direct unmanaged resources)
        // ~FilePromptProvider()
        // {
        //     Dispose(false);
        // }
    }
}