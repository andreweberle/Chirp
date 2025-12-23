using Chirp.Application.Common.EventBusOptions;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure;
using Chirp.Infrastructure.EventBus;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;

namespace Chirp.Infrastructure.Tests.Infrastructure;

[TestClass]
public class UseChirpTests
{
    private static RabbitMqContainer _rabbitmqContainer = null!;
    private static readonly string Username = "guest";
    private static readonly string Password = "guest";
    private static string _hostname = null!;
    private static int _port;
    private static int _webPort;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext testContext)
    {
        // Create a RabbitMQ container instance
        _rabbitmqContainer = new RabbitMqBuilder()
            .WithUsername(Username)
            .WithPassword(Password)
            .WithPortBinding(5672, true)
            .WithPortBinding(15672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672))
            .WithImage("rabbitmq:3.11-management")
            .Build();

        // Start the container
        await _rabbitmqContainer.StartAsync(testContext.CancellationToken);

        // Get connection details
        _hostname = _rabbitmqContainer.Hostname;
        _port = _rabbitmqContainer.GetMappedPublicPort(5672);
        _webPort = _rabbitmqContainer.GetMappedPublicPort(15672);
    }

    [ClassCleanup]
    public static async Task ClassCleanup(TestContext testContext)
    {
        if (_rabbitmqContainer != null)
        {
            await _rabbitmqContainer.StopAsync(testContext.CancellationToken);
            await _rabbitmqContainer.DisposeAsync();
        }
    }

    private IConfiguration CreateTestConfiguration()
    {
        var configData = new Dictionary<string, string>
        {
            { "RMQ:Host", "localhost" },
            { "RMQ:Username", "guest" },
            { "RMQ:Password", "guest" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)] // 5 second timeout to detect hangs
    public void UseChirp_WithIHost_DoesNotHang()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddChirp(options =>
        {
            options.EventBusType = EventBusType.RabbitMQ;
            options.QueueName = "test_host_queue";
            options.RetryCount = 3;
        }, configuration);

        var serviceProvider = services.BuildServiceProvider();
        var host = new TestHost(serviceProvider);

        // Act
        var result = host.UseChirp();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreSame(host, result);
        var eventBus = serviceProvider.GetService<IChirpEventBus>();
        Assert.IsNotNull(eventBus, "Event bus should be registered");
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public void UseChirp_WithIApplicationBuilder_DoesNotHang()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddChirp(options =>
        {
            options.EventBusType = EventBusType.RabbitMQ;
            options.QueueName = "test_app_builder_queue";
            options.RetryCount = 3;
        }, configuration);

        var serviceProvider = services.BuildServiceProvider();
        var appBuilder = new TestApplicationBuilder(serviceProvider);

        // Act
        var result = appBuilder.UseChirp();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreSame(appBuilder, result);
        var eventBus = serviceProvider.GetService<IChirpEventBus>();
        Assert.IsNotNull(eventBus, "Event bus should be registered");
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public void UseChirp_WithEventHandlers_DoesNotHang()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddChirp(options =>
        {
            options.EventBusType = EventBusType.RabbitMQ;
            options.QueueName = "test_with_handlers_queue";
            options.RetryCount = 3;
            options.AddConsumer<TestEventHandler>();
        }, configuration);

        var serviceProvider = services.BuildServiceProvider();
        var host = new TestHost(serviceProvider);

        // Act
        var result = host.UseChirp();

        // Assert
        Assert.IsNotNull(result);
        var eventBus = serviceProvider.GetService<IChirpEventBus>();
        Assert.IsNotNull(eventBus);
        
        // Verify handler is registered
        var handler = serviceProvider.GetService<TestEventHandler>();
        Assert.IsNotNull(handler, "Handler should be registered");
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public void UseChirp_CalledMultipleTimes_DoesNotHang()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddChirp(options =>
        {
            options.EventBusType = EventBusType.RabbitMQ;
            options.QueueName = "test_multiple_calls_queue";
            options.RetryCount = 3;
        }, configuration);

        var serviceProvider = services.BuildServiceProvider();
        var host = new TestHost(serviceProvider);

        // Act - Call UseChirp multiple times to ensure it's idempotent
        var result1 = host.UseChirp();
        var result2 = host.UseChirp();
        var result3 = host.UseChirp();

        // Assert
        Assert.IsNotNull(result1);
        Assert.IsNotNull(result2);
        Assert.IsNotNull(result3);
        Assert.AreSame(host, result1);
        Assert.AreSame(host, result2);
        Assert.AreSame(host, result3);
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public void UseChirp_WithDifferentOverloads_AllResolveEventBus()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddChirp(options =>
        {
            options.EventBusType = EventBusType.RabbitMQ;
            options.QueueName = "test_overloads_queue";
            options.RetryCount = 3;
        }, configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - IHost overload
        var host = new TestHost(serviceProvider);
        host.UseChirp();
        var eventBus1 = serviceProvider.GetService<IChirpEventBus>();
        Assert.IsNotNull(eventBus1, "IHost.UseChirp should resolve event bus");

        // Act & Assert - IApplicationBuilder overload
        var appBuilder = new TestApplicationBuilder(serviceProvider);
        appBuilder.UseChirp();
        var eventBus2 = serviceProvider.GetService<IChirpEventBus>();
        Assert.IsNotNull(eventBus2, "IApplicationBuilder.UseChirp should resolve event bus");

        // All should be the same singleton instance
        Assert.AreSame(eventBus1, eventBus2);
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public void UseChirp_EventBusIsSingleton_VerifyRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddChirp(options =>
        {
            options.EventBusType = EventBusType.RabbitMQ;
            options.QueueName = "test_singleton_queue";
            options.RetryCount = 3;
        }, configuration);

        var serviceProvider = services.BuildServiceProvider();
        var host = new TestHost(serviceProvider);

        // Act
        host.UseChirp();

        // Get event bus multiple times
        var eventBus1 = serviceProvider.GetRequiredService<IChirpEventBus>();
        var eventBus2 = serviceProvider.GetRequiredService<IChirpEventBus>();
        var eventBus3 = serviceProvider.GetRequiredService<IChirpEventBus>();

        // Assert
        Assert.IsNotNull(eventBus1);
        Assert.IsNotNull(eventBus2);
        Assert.IsNotNull(eventBus3);
        Assert.AreSame(eventBus1, eventBus2, "Event bus should be singleton");
        Assert.AreSame(eventBus2, eventBus3, "Event bus should be singleton");
    }

    [TestMethod]
    [Timeout(10000, CooperativeCancellation = true)] // Longer timeout for parallel execution
    public void UseChirp_CalledInParallel_DoesNotDeadlock()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddChirp(options =>
        {
            options.EventBusType = EventBusType.RabbitMQ;
            options.QueueName = "test_parallel_queue";
            options.RetryCount = 3;
        }, configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Act - Call UseChirp from multiple threads in parallel
        var tasks = new Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                var host = new TestHost(serviceProvider);
                host.UseChirp();
                var eventBus = serviceProvider.GetService<IChirpEventBus>();
                Assert.IsNotNull(eventBus);
            });
        }

        // Assert - All tasks should complete without deadlock
        Assert.IsTrue(Task.WaitAll(tasks, TimeSpan.FromSeconds(8)));
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public void UseChirp_WithRabbitMqOptions_RegistersEventBusCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddChirp((RabbitMqChirpOptions options) =>
        {
            options.Host = "localhost";
            options.Username = "guest";
            options.Password = "guest";
            options.QueueName = "test_rabbitmq_options_queue";
            options.RetryCount = 3;
            options.ExchangeName = "test_exchange";
        }, configuration);

        var serviceProvider = services.BuildServiceProvider();
        var host = new TestHost(serviceProvider);

        // Act
        var result = host.UseChirp();

        // Assert
        Assert.IsNotNull(result);
        var eventBus = serviceProvider.GetService<IChirpEventBus>();
        Assert.IsNotNull(eventBus, "Event bus should be registered");
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public void UseChirp_RetrievesEventBusWithoutBlocking()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddChirp(options =>
        {
            options.EventBusType = EventBusType.RabbitMQ;
            options.QueueName = "test_no_blocking_queue";
            options.RetryCount = 3;
        }, configuration);

        var serviceProvider = services.BuildServiceProvider();
        var host = new TestHost(serviceProvider);

        // Measure time to ensure it doesn't block
        var startTime = DateTime.UtcNow;

        // Act
        host.UseChirp();
        var eventBus = serviceProvider.GetRequiredService<IChirpEventBus>();

        var elapsedTime = DateTime.UtcNow - startTime;

        // Assert
        Assert.IsNotNull(eventBus);

        // Should complete very quickly (under 1 second) since initialization is deferred
        Assert.IsLessThan(1,elapsedTime.TotalSeconds, $"UseChirp took {elapsedTime.TotalSeconds} seconds, which is too long");
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public void UseChirp_DoesNotInitializeInfrastructureImmediately()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddChirp(options =>
        {
            options.EventBusType = EventBusType.RabbitMQ;
            options.QueueName = "test_deferred_init_queue";
            options.RetryCount = 3;
        }, configuration);

        var serviceProvider = services.BuildServiceProvider();
        var host = new TestHost(serviceProvider);

        // Act - UseChirp should complete quickly without initializing infrastructure
        var startTime = DateTime.UtcNow;
        host.UseChirp();
        var elapsedTime = DateTime.UtcNow - startTime;

        // Assert - Should complete in under 500ms since no RabbitMQ connection is made
        Assert.IsTrue(elapsedTime.TotalMilliseconds < 500,
            $"UseChirp took {elapsedTime.TotalMilliseconds}ms which suggests it's blocking on infrastructure initialization");
    }

    [TestMethod]
    [Timeout(5000, CooperativeCancellation = true)]
    public void UseChirp_MultipleServicesResolution_DoesNotHang()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddChirp(options =>
        {
            options.EventBusType = EventBusType.RabbitMQ;
            options.QueueName = "test_multi_resolution_queue";
            options.RetryCount = 3;
            options.AddConsumer<TestEventHandler>();
        }, configuration);

        var serviceProvider = services.BuildServiceProvider();
        var host = new TestHost(serviceProvider);

        // Act - UseChirp and resolve multiple services
        host.UseChirp();
        
        var eventBus = serviceProvider.GetRequiredService<IChirpEventBus>();
        var handler1 = serviceProvider.GetService<TestEventHandler>();
        var handler2 = serviceProvider.GetService<IChirpIntegrationEventHandler<TestEvent>>();
        var eventBus2 = serviceProvider.GetRequiredService<IChirpEventBus>();

        // Assert
        Assert.IsNotNull(eventBus);
        Assert.IsNotNull(handler1);
        Assert.IsNotNull(handler2);
        Assert.AreSame(eventBus, eventBus2, "Event bus should remain singleton");
    }
}

// Test helper classes
internal class TestHost : IHost
{
    public TestHost(IServiceProvider services)
    {
        Services = services;
    }

    public IServiceProvider Services { get; }

    public void Dispose() { }
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal class TestApplicationBuilder : IApplicationBuilder
{
    public TestApplicationBuilder(IServiceProvider serviceProvider)
    {
        ApplicationServices = serviceProvider;
        Properties = new Dictionary<string, object?>();
    }

    public IServiceProvider ApplicationServices { get; set; }
    public IFeatureCollection ServerFeatures => throw new NotImplementedException();
    public IDictionary<string, object?> Properties { get; }

    public RequestDelegate Build() => throw new NotImplementedException();
    public IApplicationBuilder New() => throw new NotImplementedException();
    public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware) => this;
}

// Test event and handler
internal record TestEvent(string Message) : IntegrationEvent;

internal class TestEventHandler : IChirpIntegrationEventHandler<TestEvent>
{
    public Task Handle(TestEvent @event)
    {
        return Task.CompletedTask;
    }
}
