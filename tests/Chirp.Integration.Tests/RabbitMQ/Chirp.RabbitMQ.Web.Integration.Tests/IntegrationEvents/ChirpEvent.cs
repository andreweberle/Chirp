namespace Chirp.RabbitMQ.Web.Integration.Tests.IntegrationEvents;

public record ChirpEvent(string Message) : Chirp.Domain.Common.IntegrationEvent;