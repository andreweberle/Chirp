namespace Chirp.Domain.Common;

public static class ConnectionFactory
{
    public static RabbitMQ.Client.IConnectionFactory CreateConnectionFactory
        (
            string host,
            string username, 
            string password, 
            int port,
            bool autoRecoverEnabled, 
            bool topologyRecoveryEnabled, 
            TimeSpan networkRecoveryInterval,
            TimeSpan requestedHeartbeat
            )
    {
        return new RabbitMQ.Client.ConnectionFactory
        {
            RequestedHeartbeat = requestedHeartbeat,
            HostName = host,
            Port = port,
            UserName = username,
            Password = password,
            AutomaticRecoveryEnabled = autoRecoverEnabled,
            TopologyRecoveryEnabled = topologyRecoveryEnabled,
            NetworkRecoveryInterval = networkRecoveryInterval,
        };
    }
}