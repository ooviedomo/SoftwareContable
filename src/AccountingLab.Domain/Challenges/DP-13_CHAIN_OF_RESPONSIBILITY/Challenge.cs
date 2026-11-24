using AccountingLab.Domain.Education;

namespace AccountingLab.Domain.Challenges.DP13ChainOfResponsibility;

public sealed record PaymentApprovalRequest(decimal Amount, decimal AvailableFunds, bool PolicyAllows, decimal AuthorizationLimit, IReadOnlyList<string> Checks);
public sealed record PaymentApprovalDecision(bool Approved, string? RejectedBy, IReadOnlyList<string> VisitedChecks);
public interface IPaymentApprovalChallenge { PaymentApprovalDecision Evaluate(PaymentApprovalRequest request); }
public sealed class PaymentApprovalChallenge : IPaymentApprovalChallenge
{
    public PaymentApprovalDecision Evaluate(PaymentApprovalRequest request)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount <= 0m)
            throw new ArgumentException("El monto del pago debe ser mayor que cero.", nameof(request));

        if (request.AvailableFunds < 0m)
            throw new ArgumentException("Los fondos disponibles no pueden ser negativos.", nameof(request));

        if (request.AuthorizationLimit < 0m)
            throw new ArgumentException("El límite de autorización no puede ser negativo.", nameof(request));

        List<string> visitedChecks = [];

        IPaymentApprovalHandler chain = BuildChain(request.Checks);
        return chain.Handle(request, visitedChecks);
    }

    private static IPaymentApprovalHandler BuildChain(IReadOnlyList<string>? requestedChecks)
    {
        Dictionary<string, Func<string, IPaymentApprovalHandler>> handlersByKey = new(StringComparer.OrdinalIgnoreCase)
        {
            ["fondos"] = name => new FundsAvailabilityHandler(name),
            ["funds"] = name => new FundsAvailabilityHandler(name),
            ["politica"] = name => new PolicyComplianceHandler(name),
            ["política"] = name => new PolicyComplianceHandler(name),
            ["policy"] = name => new PolicyComplianceHandler(name),
            ["limite"] = name => new AuthorizationLimitHandler(name),
            ["límite"] = name => new AuthorizationLimitHandler(name),
            ["limit"] = name => new AuthorizationLimitHandler(name)
        };

        List<IPaymentApprovalHandler> orderedHandlers = [];

        if (requestedChecks is { Count: > 0 })
        {
            foreach (string rawCheck in requestedChecks)
            {
                if (string.IsNullOrWhiteSpace(rawCheck))
                    continue;

                string key = rawCheck.Trim();
                orderedHandlers.Add(handlersByKey.TryGetValue(key, out Func<string, IPaymentApprovalHandler>? factory)
                    ? factory(key)
                    : new NamedPassThroughHandler(key));
            }
        }

        if (orderedHandlers.Count == 0)
        {
            orderedHandlers.Add(new FundsAvailabilityHandler("fondos"));
            orderedHandlers.Add(new PolicyComplianceHandler("politica"));
            orderedHandlers.Add(new AuthorizationLimitHandler("limite"));
        }

        for (int i = 0; i < orderedHandlers.Count - 1; i++)
            orderedHandlers[i].SetNext(orderedHandlers[i + 1]);

        return orderedHandlers[0];
    }

    // Handler contract
    private interface IPaymentApprovalHandler
    {
        string Name { get; }
        IPaymentApprovalHandler SetNext(IPaymentApprovalHandler next);
        PaymentApprovalDecision Handle(PaymentApprovalRequest request, List<string> visitedChecks);
    }

    // Base Handler
    private abstract class PaymentApprovalHandlerBase(string name) : IPaymentApprovalHandler
    {
        private IPaymentApprovalHandler? _next;

        public string Name { get; } = name;

        public IPaymentApprovalHandler SetNext(IPaymentApprovalHandler next)
        {
            _next = next;
            return next;
        }

        public PaymentApprovalDecision Handle(PaymentApprovalRequest request, List<string> visitedChecks)
        {
            visitedChecks.Add(Name);

            if (!CanApprove(request))
                return new PaymentApprovalDecision(false, Name, visitedChecks.AsReadOnly());

            if (_next is not null)
                return _next.Handle(request, visitedChecks);

            return new PaymentApprovalDecision(true, null, visitedChecks.AsReadOnly());
        }

        protected abstract bool CanApprove(PaymentApprovalRequest request);
    }

    // Concrete Handlers
    private sealed class FundsAvailabilityHandler(string name) : PaymentApprovalHandlerBase(name)
    {
        protected override bool CanApprove(PaymentApprovalRequest request) =>
            request.AvailableFunds >= request.Amount;
    }

    private sealed class PolicyComplianceHandler(string name) : PaymentApprovalHandlerBase(name)
    {
        protected override bool CanApprove(PaymentApprovalRequest request) =>
            request.PolicyAllows;
    }

    private sealed class AuthorizationLimitHandler(string name) : PaymentApprovalHandlerBase(name)
    {
        protected override bool CanApprove(PaymentApprovalRequest request) =>
            request.Amount <= request.AuthorizationLimit;
    }

    private sealed class NamedPassThroughHandler(string checkName) : PaymentApprovalHandlerBase(checkName)
    {
        protected override bool CanApprove(PaymentApprovalRequest request) => true;
    }
}

