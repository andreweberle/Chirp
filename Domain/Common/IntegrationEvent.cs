using System.Text.Json.Serialization;

namespace Chirp.Domain.Common;

[method: JsonConstructor]
public record IntegrationEvent(Guid Id, DateTime CreationDate)
{
    public IntegrationEvent() : this(Guid.NewGuid(), DateTime.UtcNow)
    {
    }

    [JsonInclude] public Guid Id { get; private init; } = Id;

    [JsonInclude] public DateTime CreationDate { get; private init; } = CreationDate;
}