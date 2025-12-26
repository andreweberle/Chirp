namespace Chirp.InMemory.Web.Integration.Tests.IntegrationEvents;

public record ChirpEvent(string Message, bool ThrowException) : Chirp.Domain.Common.IntegrationEvent;