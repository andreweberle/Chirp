using System.Collections.Concurrent;
using Chirp.Domain.Common;

namespace Chirp.Application.Interfaces;

public record ChirpInMemoryDeadLetterEnvelope(IntegrationEvent Event, int RetryCount,  Exception Exception);

public interface IChirpInMemoryDeadLetterQueue
{
    public IReadOnlyCollection<ChirpInMemoryDeadLetterEnvelope> GetDeadLetters();
    public void EnqueueDeadLetter(ChirpInMemoryDeadLetterEnvelope @event);
    bool TryGet(Guid id, out ChirpInMemoryDeadLetterEnvelope? @event);
}

public class InMemoryDeadLetterQueue : IChirpInMemoryDeadLetterQueue
{
    private readonly ConcurrentDictionary<Guid, ChirpInMemoryDeadLetterEnvelope> _deadLetters = [];
    
    public IReadOnlyCollection<ChirpInMemoryDeadLetterEnvelope> GetDeadLetters()
    {
        return _deadLetters.OfType<ChirpInMemoryDeadLetterEnvelope>().ToList().AsReadOnly();
    }
    public void EnqueueDeadLetter(ChirpInMemoryDeadLetterEnvelope @event)
    {
        _deadLetters.TryAdd(@event.Event.Id, @event);
    }

    /// <summary>
    /// Tries to get the dead letter event by id.
    /// </summary>
    /// <param name="id"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public bool TryGet(Guid id, out ChirpInMemoryDeadLetterEnvelope? @event)
    {
        // Attempt to find the event in the queue.
        if (_deadLetters.TryGetValue(id, out @event))
        {
            // Remove the event from the queue if found.
            _deadLetters.TryRemove(id, out _);
            
            return true;
        }
        
        @event = null;
        return false;
    }
}