using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskServer.Core.Interfaces;
using TaskServer.Infrastructure.Authorization;
using TaskServer.Infrastructure.Executors;
using TaskServer.Infrastructure.Services;
using TaskServer.Infrastructure.Storage;

namespace TaskServer.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTaskServer(this IServiceCollection services, Action<TaskServerOptions>? configureOptions = null)
    {
        services.Configure<TaskServerOptions>(options =>
        {
            configureOptions?.Invoke(options);
        });

        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IProgressAggregationService, ProgressAggregationService>();

        // Register TaskProcessorService as singleton so it can be resolved as ITaskCancellationService
        services.AddSingleton<TaskProcessorService>();
        services.AddSingleton<ITaskCancellationService>(sp => sp.GetRequiredService<TaskProcessorService>());
        services.AddHostedService(sp => sp.GetRequiredService<TaskProcessorService>());

        return services;
    }

    public static IServiceCollection AddInMemoryTaskStorage(this IServiceCollection services)
    {
        services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
        services.AddSingleton<ITaskQueue, InMemoryTaskQueue>();
        services.AddSingleton<ITaskGroupRepository, InMemoryTaskGroupRepository>();
        services.AddScoped<ITaskGroupService, TaskGroupService>();
        return services;
    }

    public static IServiceCollection AddTaskExecutor<TExecutor>(this IServiceCollection services)
        where TExecutor : class, ITaskExecutor
    {
        services.AddSingleton<ITaskExecutor, TExecutor>();
        return services;
    }

    public static IServiceCollection AddTaskServerAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, TaskOwnerAuthorizationHandler>();
        return services;
    }

    public static IServiceCollection AddTaskExecutorPlugins(
        this IServiceCollection services,
        string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
        {
            return services;
        }

        foreach (var dllPath in Directory.GetFiles(pluginsPath, "*.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllPath);
                var executorTypes = assembly.GetTypes()
                    .Where(t => typeof(ITaskExecutor).IsAssignableFrom(t)
                             && !t.IsInterface
                             && !t.IsAbstract);

                foreach (var executorType in executorTypes)
                {
                    services.AddSingleton(typeof(ITaskExecutor), executorType);
                }
            }
            catch (Exception)
            {
                // Skip DLLs that can't be loaded (dependencies, native libs, etc.)
            }
        }

        return services;
    }
}
