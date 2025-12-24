namespace Chirp.Application.Common.EventBusOptions;

public class InMemoryOptions : ChirpOptions
{
    public InMemoryOptions()
    {
        EventBusType = EventBusType.InMemory;
    }
}