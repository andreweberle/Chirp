using Chirp.Application.Interfaces;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using System.Runtime.CompilerServices;
using Testcontainers.RabbitMq;
using ConnectionFactory = RabbitMQ.Client.ConnectionFactory;

[assembly: InternalsVisibleTo("Chirp.Infrastructure.Tests")]

namespace Chirp.Infrastructure.Tests.RabbitMQ;

[TestClass]
public class RabbitMqConnectionTests
{
    private RabbitMqContainer? _rabbitMqContainer;
    private ConnectionFactory? _connectionFactory;
    private IRabbitMqConnection? _rabbitMqConnection;

    [TestInitialize]
    public async Task Initialize()
    {
        // Set up the RabbitMQ test container
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .WithPortBinding(5672, true)
            .Build();

        // Start the container
        await _rabbitMqContainer.StartAsync();

        // Create a connection factory using the container's connection info
        _connectionFactory = new ConnectionFactory
        {
            HostName = _rabbitMqContainer.Hostname,
            Port = _rabbitMqContainer.GetMappedPublicPort(5672),
            UserName = "testuser",
            Password = "testpassword",
            RequestedHeartbeat = TimeSpan.FromSeconds(10),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };

        // Create the RabbitMQ connection
        _rabbitMqConnection = new RabbitMqConnection(_connectionFactory);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_rabbitMqConnection is IDisposable disposableConnection)
        {
            disposableConnection.Dispose();
        }

        if (_rabbitMqContainer != null)
        {
            await _rabbitMqContainer.DisposeAsync();
        }
    }

    [TestMethod]
    public void CreateModel_WhenConnected_ReturnsModel()
    {
        // Arrange
        _rabbitMqConnection?.TryConnect();

        // Act
        var model = _rabbitMqConnection?.CreateModel();

        // Assert
        Assert.IsNotNull(model);
        Assert.IsTrue(model.IsOpen);

        // Clean up
        model?.Dispose();
    }

    [TestMethod]
    public void IsConnected_AfterTryConnect_ReturnsTrue()
    {
        // Arrange & Act
        _rabbitMqConnection?.TryConnect();

        // Assert
        Assert.IsTrue(_rabbitMqConnection?.IsConnected);
    }

    [TestMethod]
    public void TryConnect_WhenCalled_EstablishesConnection()
    {
        // Act
        _rabbitMqConnection?.TryConnect();

        // Assert
        Assert.IsTrue(_rabbitMqConnection?.IsConnected);
    }
}