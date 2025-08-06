// ReSharper disable once CheckNamespace
namespace BD.WTTS.Resources;

/// <summary>
/// Lazy resource loader for improved startup performance and reduced memory usage
/// </summary>
public static class LazyResourceLoader
{
    private static readonly ConcurrentDictionary<string, Lazy<object?>> _resourceCache = new();
    private static readonly Dictionary<string, ResourceInfo> _resourceManifest = new();
    private static bool _manifestLoaded = false;

    /// <summary>
    /// Resource information for prioritized loading
    /// </summary>
    private record ResourceInfo(string Name, long Size, ResourcePriority Priority);

    /// <summary>
    /// Resource loading priority
    /// </summary>
    public enum ResourcePriority
    {
        Critical,  // Load immediately
        High,      // Load on first access
        Normal,    // Load on demand
        Low        // Load in background
    }

    /// <summary>
    /// Initialize the resource loader with manifest
    /// </summary>
    public static void Initialize()
    {
        if (_manifestLoaded) return;

        try
        {
            LoadResourceManifest();
            PreloadCriticalResources();
            _manifestLoaded = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize resource loader: {ex.Message}");
        }
    }

    /// <summary>
    /// Get a resource lazily with caching
    /// </summary>
    public static T? GetResource<T>(string resourceName) where T : class
    {
        var lazy = _resourceCache.GetOrAdd(resourceName, name => 
            new Lazy<object?>(() => LoadResourceInternal(name), LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value as T;
    }

    /// <summary>
    /// Get a resource asynchronously
    /// </summary>
    public static async Task<T?> GetResourceAsync<T>(string resourceName) where T : class
    {
        // Check if already cached
        if (_resourceCache.TryGetValue(resourceName, out var existingLazy) && existingLazy.IsValueCreated)
        {
            return existingLazy.Value as T;
        }

        // Load asynchronously
        return await Task.Run(() => GetResource<T>(resourceName));
    }

    /// <summary>
    /// Preload resources by priority
    /// </summary>
    public static async Task PreloadResourcesAsync(ResourcePriority priority)
    {
        var resourcesToLoad = _resourceManifest
            .Where(kvp => kvp.Value.Priority == priority)
            .Select(kvp => kvp.Key)
            .ToArray();

        if (resourcesToLoad.Length == 0) return;

        await Task.Run(() =>
        {
            Parallel.ForEach(resourcesToLoad, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, resourceName =>
            {
                try
                {
                    GetResource<object>(resourceName);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to preload resource {resourceName}: {ex.Message}");
                }
            });
        });
    }

    /// <summary>
    /// Get resource size information
    /// </summary>
    public static long GetResourceSize(string resourceName)
    {
        return _resourceManifest.TryGetValue(resourceName, out var info) ? info.Size : 0;
    }

    /// <summary>
    /// Clear cached resources to free memory
    /// </summary>
    public static void ClearCache()
    {
        _resourceCache.Clear();
        GC.Collect();
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public static (int CachedCount, int TotalCount, long EstimatedMemoryUsage) GetCacheStats()
    {
        var cachedCount = _resourceCache.Count(kvp => kvp.Value.IsValueCreated);
        var totalCount = _resourceCache.Count;
        var estimatedMemory = _resourceCache
            .Where(kvp => kvp.Value.IsValueCreated)
            .Sum(kvp => GetResourceSize(kvp.Key));

        return (cachedCount, totalCount, estimatedMemory);
    }

    #region Private Methods

    private static void LoadResourceManifest()
    {
        try
        {
            var manifestPath = Path.Combine(AppContext.BaseDirectory, "ResourceManifest.txt");
            if (!File.Exists(manifestPath)) return;

            var lines = File.ReadAllLines(manifestPath);
            foreach (var line in lines)
            {
                var parts = line.Split(';');
                if (parts.Length >= 3)
                {
                    var name = parts[0];
                    var size = long.TryParse(parts[1], out var s) ? s : 0;
                    var priority = Enum.TryParse<ResourcePriority>(parts[2], out var p) ? p : ResourcePriority.Normal;

                    _resourceManifest[name] = new ResourceInfo(name, size, priority);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load resource manifest: {ex.Message}");
        }
    }

    private static void PreloadCriticalResources()
    {
        var criticalResources = _resourceManifest
            .Where(kvp => kvp.Value.Priority == ResourcePriority.Critical)
            .Select(kvp => kvp.Key);

        foreach (var resourceName in criticalResources)
        {
            try
            {
                GetResource<object>(resourceName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to preload critical resource {resourceName}: {ex.Message}");
            }
        }
    }

    private static object? LoadResourceInternal(string resourceName)
    {
        try
        {
            // Try to load from embedded resources first
            var assembly = Assembly.GetExecutingAssembly();
            var resourceStream = assembly.GetManifestResourceStream(resourceName);
            
            if (resourceStream != null)
            {
                using (resourceStream)
                {
                    // Determine resource type and load accordingly
                    if (resourceName.EndsWith(".png") || resourceName.EndsWith(".jpg") || resourceName.EndsWith(".ico"))
                    {
                        return LoadImageResource(resourceStream);
                    }
                    else if (resourceName.EndsWith(".resx"))
                    {
                        return LoadStringResource(resourceStream);
                    }
                    else
                    {
                        // Generic binary resource
                        using var ms = new MemoryStream();
                        resourceStream.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
            }

            // Fallback to file system
            var filePath = Path.Combine(AppContext.BaseDirectory, resourceName);
            if (File.Exists(filePath))
            {
                return File.ReadAllBytes(filePath);
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load resource {resourceName}: {ex.Message}");
            return null;
        }
    }

    private static object? LoadImageResource(Stream stream)
    {
        try
        {
            // Return as byte array for now - UI framework can convert as needed
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load image resource: {ex.Message}");
            return null;
        }
    }

    private static object? LoadStringResource(Stream stream)
    {
        try
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load string resource: {ex.Message}");
            return null;
        }
    }

    #endregion
}

/// <summary>
/// Resource loading extensions for common scenarios
/// </summary>
public static class ResourceExtensions
{
    /// <summary>
    /// Load image resource with caching
    /// </summary>
    public static async Task<byte[]?> LoadImageAsync(this string resourceName)
    {
        return await LazyResourceLoader.GetResourceAsync<byte[]>(resourceName);
    }

    /// <summary>
    /// Load string resource with caching
    /// </summary>
    public static async Task<string?> LoadStringAsync(this string resourceName)
    {
        return await LazyResourceLoader.GetResourceAsync<string>(resourceName);
    }

    /// <summary>
    /// Preload UI-critical resources
    /// </summary>
    public static async Task PreloadUICriticalResourcesAsync()
    {
        await LazyResourceLoader.PreloadResourcesAsync(LazyResourceLoader.ResourcePriority.Critical);
        await LazyResourceLoader.PreloadResourcesAsync(LazyResourceLoader.ResourcePriority.High);
    }
}