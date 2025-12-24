using Chirp.Infrastructure;
using Chirp.RabbitMQ.Web.Integration.Tests;
using Chirp.RabbitMQ.Web.Integration.Tests.Handlers;
using DotNet.Testcontainers.Builders;
using Testcontainers.RabbitMq;

RabbitMqContainer _rabbitmqContainer = null!;

string Username = "guest";
string Password = "guest";
string _hostname = null!;
int _port = 0;
int _webPort;

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
await _rabbitmqContainer.StartAsync();

// Get connection details
_hostname = _rabbitmqContainer.Hostname;
        
// Standard AMQP port.
_port = _rabbitmqContainer.GetMappedPublicPort(5672);
        
// Web UI port allows us to connect to RabbitMQ management plugin api.
_webPort = _rabbitmqContainer.GetMappedPublicPort(15672);

// Create HostBuilder
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Add services to the container.

// This worker will send random messages and handle them.
builder.Services.AddHostedService<ChirpBackgroundWorker>();

// Add Chirp, This is the message broker for the LithoShipping Background Service.
builder.Services.AddChirp(options => 
    {
        // Assign the host.
        options.Host = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true" ? "localhost" : "host.docker.internal";
        options.Port = _port;
        options.Username = "guest";
        options.Password = "guest";
        options.QueueName = "Chirp.RabbitMQ.Web.Integration.Tests.Queue";
        options.RetryCount = 5;
        options.ExchangeName = "Chirp.RabbitMQ.Web.Integration.Tests.Exchange";
        options.DeadLetterExchangeName = "Chirp.RabbitMQ.Web.Integration.Tests.DLX.Exchange";
        options.AutomaticRecoveryEnabled = true;
        options.TopologyRecoveryEnabled = true;
        options.NetworkRecoveryInterval = TimeSpan.FromSeconds(5);
        options.RequestedHeartbeat = TimeSpan.FromSeconds(60);

        // Do not auto subscribe consumers.
        options.AutoSubscribeConsumers = true;

        // Add consumers.
        options.AddConsumer<NewMessageEventHandler>();
    }, 
    builder.Configuration);

// Build the host
IHost host = builder.Build();

host.UseChirp();

// Run the host
host.Run();