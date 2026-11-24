using AccountingLab.Domain.Education;

namespace AccountingLab.Domain.Challenges.DP16Mediator;

public sealed record InvoiceProcessingRequest(Guid InvoiceId, decimal Amount, bool IsValid, IReadOnlyList<string> Participants);
public sealed record InvoiceProcessingResult(Guid InvoiceId, bool Published, IReadOnlyList<string> ParticipantsInvoked);
public interface IInvoiceCoordinationChallenge { InvoiceProcessingResult Process(InvoiceProcessingRequest request); }
public sealed class InvoiceCoordinationChallenge : IInvoiceCoordinationChallenge
{
    public InvoiceProcessingResult Process(InvoiceProcessingRequest request)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);

        if (request.InvoiceId == Guid.Empty)
            throw new ArgumentException("El identificador de factura es obligatorio.", nameof(request));

        if (request.Amount <= 0m)
            throw new ArgumentException("El monto de la factura debe ser mayor que cero.", nameof(request));

        InvoicePublicationMediator mediator = new();

        ValidationParticipant validator = new(mediator);
        LedgerBookingParticipant ledger = new(mediator);
        TaxCalculationParticipant tax = new(mediator);
        PublicationParticipant publisher = new(mediator);

        mediator.Register(validator, ledger, tax, publisher);

        return mediator.Coordinate(request);
    }

    // Mediator interface
    private interface IInvoiceMediator
    {
        void Notify(InvoiceParticipant sender, string eventName, InvoiceProcessingRequest request, ProcessingState state);
    }

    private sealed class ProcessingState
    {
        public List<string> Invoked { get; } = [];
        public bool ValidationFailed { get; set; }
        public bool LedgerBooked { get; set; }
        public decimal CalculatedTax { get; set; }
        public bool Published { get; set; }
    }

    // Concrete Mediator
    private sealed class InvoicePublicationMediator : IInvoiceMediator
    {
        private readonly Dictionary<string, InvoiceParticipant> _participants = new(StringComparer.OrdinalIgnoreCase);

        public void Register(params InvoiceParticipant[] participants)
        {
            foreach (InvoiceParticipant participant in participants)
            {
                _participants[participant.Name] = participant;
            }
        }

        public InvoiceProcessingResult Coordinate(InvoiceProcessingRequest request)
        {
            ProcessingState state = new();

            IReadOnlyList<string> sequence = request.Participants is { Count: > 0 }
                ? request.Participants
                : ["validation", "ledger", "publication"];

            HashSet<string> seenParticipants = new(StringComparer.OrdinalIgnoreCase);

            foreach (string rawParticipant in sequence)
            {
                if (string.IsNullOrWhiteSpace(rawParticipant))
                    throw new ArgumentException("La lista de participantes contiene un nombre vacío o nulo.", nameof(request));

                string key = rawParticipant.Trim();
                string canonicalKey = NormalizeKey(key);

                if (!seenParticipants.Add(canonicalKey))
                    throw new ArgumentException($"Participante duplicado en la coordinación: '{key}'.", nameof(request));

                if (_participants.TryGetValue(canonicalKey, out InvoiceParticipant? participant))
                {
                    participant.Receive("Execute", request, state, key);
                }
                else
                {
                    state.Invoked.Add(key);
                }

                if (state.ValidationFailed)
                    break;
            }

            if (state.ValidationFailed)
                state.Published = false;

            return new InvoiceProcessingResult(
                request.InvoiceId,
                state.Published,
                state.Invoked.AsReadOnly());
        }

        public void Notify(InvoiceParticipant sender, string eventName, InvoiceProcessingRequest request, ProcessingState state)
        {
            if (eventName == "ValidationFailed")
            {
                state.ValidationFailed = true;
                state.Published = false;
            }
            else if (eventName == "LedgerBooked")
            {
                state.LedgerBooked = true;
            }
            else if (eventName == "TaxCalculated")
            {
                state.CalculatedTax = decimal.Round(request.Amount * 0.19m, 2, MidpointRounding.AwayFromZero);
            }
            else if (eventName == "Published" && !state.ValidationFailed)
            {
                state.Published = true;
            }
        }

        private static string NormalizeKey(string key) => key.ToLowerInvariant() switch
        {
            "validation" or "validator" or "validacion" or "validación" => "validation",
            "ledger" or "accounting" or "contabilidad" or "contabilizacion" or "contabilización" or "mayor" => "ledger",
            "tax" or "taxes" or "impuestos" => "tax",
            "publication" or "publisher" or "publish" or "publicacion" or "publicación" => "publication",
            _ => key.ToLowerInvariant()
        };
    }

    // Colleague base
    private abstract class InvoiceParticipant(IInvoiceMediator mediator)
    {
        protected IInvoiceMediator Mediator { get; } = mediator;
        public abstract string Name { get; }

        public virtual void Receive(string signal, InvoiceProcessingRequest request, ProcessingState state, string reportedName)
        {
            state.Invoked.Add(reportedName);
            OnExecute(request, state);
        }

        protected abstract void OnExecute(InvoiceProcessingRequest request, ProcessingState state);
    }

    // Concrete Colleagues
    private sealed class ValidationParticipant(IInvoiceMediator mediator) : InvoiceParticipant(mediator)
    {
        public override string Name => "validation";

        protected override void OnExecute(InvoiceProcessingRequest request, ProcessingState state)
        {
            if (!request.IsValid)
                Mediator.Notify(this, "ValidationFailed", request, state);
        }
    }

    private sealed class LedgerBookingParticipant(IInvoiceMediator mediator) : InvoiceParticipant(mediator)
    {
        public override string Name => "ledger";

        protected override void OnExecute(InvoiceProcessingRequest request, ProcessingState state)
        {
            Mediator.Notify(this, "LedgerBooked", request, state);
        }
    }

    private sealed class TaxCalculationParticipant(IInvoiceMediator mediator) : InvoiceParticipant(mediator)
    {
        public override string Name => "tax";

        protected override void OnExecute(InvoiceProcessingRequest request, ProcessingState state)
        {
            Mediator.Notify(this, "TaxCalculated", request, state);
        }
    }

    private sealed class PublicationParticipant(IInvoiceMediator mediator) : InvoiceParticipant(mediator)
    {
        public override string Name => "publication";

        protected override void OnExecute(InvoiceProcessingRequest request, ProcessingState state)
        {
            Mediator.Notify(this, "Published", request, state);
        }
    }
}