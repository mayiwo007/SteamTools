// ReSharper disable once CheckNamespace
namespace BD.WTTS;

/// <summary>
/// Optimized service extensions for improved startup performance
/// </summary>
public static class OptimizedServiceExtensions
{
    /// <summary>
    /// Add services with lazy initialization for better startup performance
    /// </summary>
    public static IServiceCollection AddOptimizedServices(this IServiceCollection services)
    {
        // Register lazy singletons for heavy services
        services.AddLazySingleton<ISteamService>();
        services.AddLazySingleton<IGameAccountPlatformAuthenticatorService>();
        services.AddLazySingleton<IArchiSteamFarmService>();
        services.AddLazySingleton<ICloudServiceClient>();
        services.AddLazySingleton<IApplicationUpdateService>();
        
        // Background services that don't need immediate initialization
        services.AddLazyBackgroundService<IHostsFileService>();
        services.AddLazyBackgroundService<IScriptService>();
        services.AddLazyBackgroundService<IReverseProxyService>();
        
        return services;
    }

    /// <summary>
    /// Register a service as lazy singleton
    /// </summary>
    private static IServiceCollection AddLazySingleton<TService>(this IServiceCollection services)
        where TService : class
    {
        services.AddSingleton<Lazy<TService>>(provider => 
            new Lazy<TService>(() => provider.GetRequiredService<TService>(), LazyThreadSafetyMode.ExecutionAndPublication));
        
        return services;
    }

    /// <summary>
    /// Register a background service as lazy
    /// </summary>
    private static IServiceCollection AddLazyBackgroundService<TService>(this IServiceCollection services)
        where TService : class
    {
        services.AddSingleton<Lazy<TService>>(provider => 
            new Lazy<TService>(() => 
            {
                // Initialize on background thread to avoid blocking UI
                return Task.Run(() => provider.GetRequiredService<TService>()).GetAwaiter().GetResult();
            }, LazyThreadSafetyMode.ExecutionAndPublication));
        
        return services;
    }

    /// <summary>
    /// Preload critical services on background thread after UI is shown
    /// </summary>
    public static async Task PreloadCriticalServicesAsync(IServiceProvider serviceProvider)
    {
        // Use background thread for service initialization
        await Task.Run(async () =>
        {
            try
            {
                // Preload services in order of importance
                var criticalServices = new[]
                {
                    typeof(Lazy<ISteamService>),
                    typeof(Lazy<ICloudServiceClient>),
                    typeof(Lazy<IApplicationUpdateService>)
                };

                // Initialize critical services with delay to spread CPU load
                foreach (var serviceType in criticalServices)
                {
                    try
                    {
                        var lazyService = serviceProvider.GetService(serviceType);
                        if (lazyService is ILazy lazy)
                        {
                            _ = lazy.Value; // Force initialization
                        }
                        
                        // Small delay to avoid CPU spikes
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail startup
                        Debug.WriteLine($"Failed to preload service {serviceType.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during service preloading: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Interface for lazy services
    /// </summary>
    private interface ILazy
    {
        object Value { get; }
    }
}

/// <summary>
/// Optimized startup helper for reducing initialization overhead
/// </summary>
public static class StartupOptimizer
{
    private static bool _isOptimized = false;
    
    /// <summary>
    /// Apply startup optimizations
    /// </summary>
    public static void OptimizeStartup()
    {
        if (_isOptimized) return;
        
        // GC optimization for startup
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        
        // ThreadPool optimization
        ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount);
        
        // JIT optimization hints
        RuntimeHelpers.PrepareConstrainedRegions();
        
        _isOptimized = true;
    }

    /// <summary>
    /// Optimize for runtime after startup is complete
    /// </summary>
    public static void OptimizeRuntime()
    {
        Task.Run(() =>
        {
            // Switch to balanced GC mode after startup
            GCSettings.LatencyMode = GCLatencyMode.Interactive;
            
            // Force GC to clean up startup allocations
            GC.Collect(2, GCCollectionMode.Optimized);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Optimized);
            
            // Compact LOH if available
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
        });
    }

    /// <summary>
    /// Precompile critical paths for better performance
    /// </summary>
    public static void PrecompileCriticalPaths()
    {
        Task.Run(() =>
        {
            try
            {
                // Force JIT compilation of critical paths
                RuntimeHelpers.PrepareMethod(typeof(Avalonia.Application).GetMethod("Current")?.MethodHandle ?? default);
                RuntimeHelpers.PrepareMethod(typeof(ReactiveObject).GetMethod("RaisePropertyChanged", new[] { typeof(string) })?.MethodHandle ?? default);
                
                // Precompile common UI operations
                var dummyObservable = Observable.Empty<object>();
                dummyObservable.Subscribe(_ => { });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during critical path precompilation: {ex.Message}");
            }
        });
    }
}