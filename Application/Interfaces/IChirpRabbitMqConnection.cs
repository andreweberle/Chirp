using RabbitMQ.Client;

namespace Chirp.Application.Interfaces;

public interface IChirpRabbitMqConnection
{
    public bool IsConnected { get; }
    public Task<bool> TryConnectAsync(CancellationToken cancellationToken = default);
    public Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default);
}