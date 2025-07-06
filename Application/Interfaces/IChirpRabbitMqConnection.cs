using RabbitMQ.Client;

namespace Chirp.Application.Interfaces;

public interface IChirpRabbitMqConnection
{
    public bool IsConnected { get; }
    public void TryConnect();
    public IModel CreateModel();
}