using AccountingLab.Domain.Education;

namespace AccountingLab.Domain.Challenges.DP18Observer;

public sealed record FinancialEvent(Guid EventId, string Kind, decimal Amount);
public interface IFinancialNotificationsChallenge
{
    void Subscribe(string subscriberId, Action<FinancialEvent> receive);
    void Unsubscribe(string subscriberId);
    IReadOnlyList<string> Publish(FinancialEvent financialEvent);
}
public sealed class FinancialNotificationsChallenge : IFinancialNotificationsChallenge
{
    private readonly FinancialEventPublisher _publisher = new();

    public void Subscribe(string subscriberId, Action<FinancialEvent> receive)
    {
        // Solucionado
        if (string.IsNullOrWhiteSpace(subscriberId))
            throw new ArgumentException("El identificador del suscriptor es obligatorio.", nameof(subscriberId));

        ArgumentNullException.ThrowIfNull(receive);

        IFinancialEventObserver observer = new DelegateFinancialEventObserver(subscriberId.Trim(), receive);
        _publisher.Attach(observer);
    }

    public void Unsubscribe(string subscriberId)
    {
        if (string.IsNullOrWhiteSpace(subscriberId))
            throw new ArgumentException("El identificador del suscriptor es obligatorio.", nameof(subscriberId));

        _publisher.Detach(subscriberId.Trim());
    }

    public IReadOnlyList<string> Publish(FinancialEvent financialEvent)
    {
        ArgumentNullException.ThrowIfNull(financialEvent);

        if (financialEvent.EventId == Guid.Empty)
            throw new ArgumentException("El identificador del evento financiero es obligatorio.", nameof(financialEvent));

        if (string.IsNullOrWhiteSpace(financialEvent.Kind))
            throw new ArgumentException("El tipo de evento financiero es obligatorio.", nameof(financialEvent));

        if (financialEvent.Amount < 0m)
            throw new ArgumentException("El monto del evento financiero no puede ser negativo.", nameof(financialEvent));

        return _publisher.Notify(financialEvent);
    }

    // Subject contract
    private interface IFinancialEventSubject
    {
        void Attach(IFinancialEventObserver observer);
        void Detach(string subscriberId);
        IReadOnlyList<string> Notify(FinancialEvent financialEvent);
    }

    // Observer contract
    private interface IFinancialEventObserver
    {
        string SubscriberId { get; }
        void OnNext(FinancialEvent financialEvent);
    }

    // Concrete Observer
    private sealed class DelegateFinancialEventObserver(
        string subscriberId,
        Action<FinancialEvent> receive) : IFinancialEventObserver
    {
        public string SubscriberId { get; } = subscriberId;

        public void OnNext(FinancialEvent financialEvent) => receive(financialEvent);
    }

    // Concrete Subject
    private sealed class FinancialEventPublisher : IFinancialEventSubject
    {
        private readonly object _syncLock = new();
        private readonly List<IFinancialEventObserver> _observers = [];

        public void Attach(IFinancialEventObserver observer)
        {
            ArgumentNullException.ThrowIfNull(observer);

            lock (_syncLock)
            {
                for (int i = 0; i < _observers.Count; i++)
                {
                    if (string.Equals(_observers[i].SubscriberId, observer.SubscriberId, StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException($"Ya existe un suscriptor registrado con el identificador '{observer.SubscriberId}'.", nameof(observer));
                }

                _observers.Add(observer);
            }
        }

        public void Detach(string subscriberId)
        {
            lock (_syncLock)
            {
                for (int i = _observers.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(_observers[i].SubscriberId, subscriberId, StringComparison.OrdinalIgnoreCase))
                    {
                        _observers.RemoveAt(i);
                    }
                }
            }
        }

        public IReadOnlyList<string> Notify(FinancialEvent financialEvent)
        {
            IFinancialEventObserver[] snapshot;
            lock (_syncLock)
            {
                snapshot = [.. _observers];
            }

            List<string> notifiedIds = [];
            foreach (IFinancialEventObserver observer in snapshot)
            {
                observer.OnNext(financialEvent);
                notifiedIds.Add(observer.SubscriberId);
            }

            return notifiedIds.AsReadOnly();
        }
    }
}