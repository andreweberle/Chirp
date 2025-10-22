using RabbitMQ.Client;

namespace Chirp.Application.Interfaces;

public interface IChirpRabbitMqConnection
{
    public bool IsConnected { get; }
    public bool TryConnect();
    public IModel CreateModel();
}