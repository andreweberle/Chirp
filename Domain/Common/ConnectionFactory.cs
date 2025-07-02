namespace Chirp.Domain.Common;

public static class ConnectionFactory
{
    public static RabbitMQ.Client.IConnectionFactory CreateConnectionFactory(string host, string username,
        string password)
    {
        return new RabbitMQ.Client.ConnectionFactory
        {
            RequestedHeartbeat = new TimeSpan(0, 0, 60),
            HostName = host,
            UserName = username,
            Password = password,
            DispatchConsumersAsync = true
        };
    }
}