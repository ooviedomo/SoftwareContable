using AccountingLab.Domain.Education;

namespace AccountingLab.Domain.Challenges.DP19State;

public sealed record InvoiceLifecycleRequest(string CurrentState, string Action, decimal OutstandingAmount, DateOnly DueDate, DateOnly BusinessDate);
public sealed record InvoiceTransitionResult(string State, bool PaymentAllowed);
public interface IInvoiceLifecycleChallenge { InvoiceTransitionResult Transition(InvoiceLifecycleRequest request); }
public sealed class InvoiceLifecycleChallenge : IInvoiceLifecycleChallenge
{
    public InvoiceTransitionResult Transition(InvoiceLifecycleRequest request)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.CurrentState))
            throw new ArgumentException("El estado actual de la factura es obligatorio.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Action))
            throw new ArgumentException("La acción del ciclo de vida es obligatoria.", nameof(request));

        if (request.OutstandingAmount < 0m)
            throw new ArgumentException("El saldo pendiente no puede ser negativo.", nameof(request));

        InvoiceLifecycleContext context = new(request);
        context.ApplyAction(request.Action);
        return context.ToResult();
    }

    // Context
    private sealed class InvoiceLifecycleContext
    {
        private IInvoiceState _state;

        public InvoiceLifecycleContext(InvoiceLifecycleRequest request)
        {
            Request = request;
            OutstandingAmount = request.OutstandingAmount;
            _state = ResolveState(request.CurrentState);
        }

        public InvoiceLifecycleRequest Request { get; }
        public decimal OutstandingAmount { get; set; }
        public bool PaymentAllowed { get; set; }

        public void SetState(IInvoiceState nextState)
        {
            _state = nextState;
        }

        public void ApplyAction(string action)
        {
            string normalizedAction = NormalizeAction(action);
            _state.HandleAction(this, normalizedAction);
        }

        public InvoiceTransitionResult ToResult() =>
            new(_state.Name, PaymentAllowed);

        private static IInvoiceState ResolveState(string rawState) => rawState.Trim().ToLowerInvariant() switch
        {
            "draft" or "borrador" => new DraftInvoiceState(),
            "issued" or "open" or "pending" or "emitida" or "abierta" or "pendiente" => new IssuedInvoiceState(),
            "overdue" or "vencida" => new OverdueInvoiceState(),
            "paid" or "settled" or "pagada" or "saldada" => new PaidInvoiceState(),
            "void" or "voided" or "cancelled" or "canceled" or "anulada" or "cancelada" => new VoidInvoiceState(),
            _ => throw new ArgumentException($"Estado de factura no soportado: '{rawState}'.", nameof(rawState))
        };

        private static string NormalizeAction(string action) => action.Trim().ToLowerInvariant() switch
        {
            "issue" or "emit" or "publish" or "emitir" or "publicar" => "issue",
            "pay" or "settle" or "payment" or "pagar" or "abonar" => "pay",
            "mark-overdue" or "overdue" or "expire" or "vencer" or "marcar-vencida" => "overdue",
            "void" or "cancel" or "anular" or "cancelar" => "void",
            "check" or "inspect" or "evaluate" or "consultar" => "check",
            _ => throw new ArgumentException($"Acción de ciclo de vida no soportada: '{action}'.", nameof(action))
        };
    }

    // State interface
    private interface IInvoiceState
    {
        string Name { get; }
        void HandleAction(InvoiceLifecycleContext context, string action);
    }

    // Concrete States
    private sealed class DraftInvoiceState : IInvoiceState
    {
        public string Name => "draft";

        public void HandleAction(InvoiceLifecycleContext context, string action)
        {
            switch (action)
            {
                case "issue":
                    bool isPastDue = context.Request.BusinessDate > context.Request.DueDate;
                    context.SetState(isPastDue ? new OverdueInvoiceState() : new IssuedInvoiceState());
                    context.PaymentAllowed = true;
                    break;

                case "void":
                    context.SetState(new VoidInvoiceState());
                    context.PaymentAllowed = false;
                    break;

                case "check":
                    context.PaymentAllowed = false;
                    break;

                default:
                    throw new InvalidOperationException($"La acción '{action}' no está permitida desde el estado '{Name}'.");
            }
        }
    }

    private sealed class IssuedInvoiceState : IInvoiceState
    {
        public string Name => "issued";

        public void HandleAction(InvoiceLifecycleContext context, string action)
        {
            switch (action)
            {
                case "pay":
                    EnsureNonNegativeBalance(context);
                    context.OutstandingAmount = 0m;
                    context.SetState(new PaidInvoiceState());
                    context.PaymentAllowed = false;
                    break;

                case "overdue":
                    if (context.Request.BusinessDate > context.Request.DueDate)
                    {
                        context.SetState(new OverdueInvoiceState());
                        context.PaymentAllowed = context.OutstandingAmount > 0m;
                    }
                    else
                    {
                        context.PaymentAllowed = context.OutstandingAmount > 0m;
                    }
                    break;

                case "void":
                    context.SetState(new VoidInvoiceState());
                    context.PaymentAllowed = false;
                    break;

                case "check":
                    if (context.Request.BusinessDate > context.Request.DueDate && context.OutstandingAmount > 0m)
                    {
                        context.SetState(new OverdueInvoiceState());
                        context.PaymentAllowed = true;
                    }
                    else
                    {
                        context.PaymentAllowed = context.OutstandingAmount > 0m;
                    }
                    break;

                default:
                    throw new InvalidOperationException($"La acción '{action}' no está permitida desde el estado '{Name}'.");
            }
        }
    }

    private sealed class OverdueInvoiceState : IInvoiceState
    {
        public string Name => "overdue";

        public void HandleAction(InvoiceLifecycleContext context, string action)
        {
            switch (action)
            {
                case "pay":
                    EnsureNonNegativeBalance(context);
                    context.OutstandingAmount = 0m;
                    context.SetState(new PaidInvoiceState());
                    context.PaymentAllowed = false;
                    break;

                case "void":
                    context.SetState(new VoidInvoiceState());
                    context.PaymentAllowed = false;
                    break;

                case "check":
                case "overdue":
                    context.PaymentAllowed = context.OutstandingAmount > 0m;
                    break;

                default:
                    throw new InvalidOperationException($"La acción '{action}' no está permitida desde el estado '{Name}'.");
            }
        }
    }

    private sealed class PaidInvoiceState : IInvoiceState
    {
        public string Name => "paid";

        public void HandleAction(InvoiceLifecycleContext context, string action)
        {
            if (action == "check")
            {
                context.PaymentAllowed = false;
                return;
            }

            throw new InvalidOperationException($"La acción '{action}' no está permitida en una factura ya pagada ('{Name}').");
        }
    }

    private sealed class VoidInvoiceState : IInvoiceState
    {
        public string Name => "void";

        public void HandleAction(InvoiceLifecycleContext context, string action)
        {
            if (action == "check")
            {
                context.PaymentAllowed = false;
                return;
            }

            throw new InvalidOperationException($"La acción '{action}' no está permitida en una factura anulada ('{Name}').");
        }
    }

    private static void EnsureNonNegativeBalance(InvoiceLifecycleContext context)
    {
        if (context.OutstandingAmount < 0m)
            throw new InvalidOperationException("El saldo pendiente no puede ser negativo.");
    }
}