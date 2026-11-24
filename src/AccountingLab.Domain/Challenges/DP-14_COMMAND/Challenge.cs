using AccountingLab.Domain.Education;

namespace AccountingLab.Domain.Challenges.DP14Command;

public sealed record AccountingOperationRequest(Guid CompanyId, Guid OperationId, string Kind, Guid TargetId, decimal Amount);
public sealed record AccountingOperationReceipt(Guid OperationId, Guid TargetId, string Kind, decimal AppliedAmount, decimal RemainingAmount, Guid LinkedJournalEntryId, bool Replayed);
public sealed record AccountingOperationContext(decimal OutstandingAmount, bool TargetIsPublished, IReadOnlyList<AccountingOperationReceipt> CompletedOperations);
public interface IIdentifiedOperationChallenge { AccountingOperationReceipt Execute(AccountingOperationRequest request, AccountingOperationContext context); }
public sealed class IdentifiedOperationChallenge : IIdentifiedOperationChallenge
{
    public AccountingOperationReceipt Execute(AccountingOperationRequest request, AccountingOperationContext context)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (request.CompanyId == Guid.Empty)
            throw new ArgumentException("El identificador de empresa es obligatorio.", nameof(request));

        if (request.OperationId == Guid.Empty)
            throw new ArgumentException("El identificador de operación es obligatorio.", nameof(request));

        if (request.TargetId == Guid.Empty)
            throw new ArgumentException("El identificador de destino es obligatorio.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Kind))
            throw new ArgumentException("El tipo de operación es obligatorio.", nameof(request));

        if (request.Amount <= 0m)
            throw new ArgumentException("El monto de la operación debe ser mayor que cero.", nameof(request));

        if (context.OutstandingAmount < 0m)
            throw new ArgumentException("El saldo pendiente no puede ser negativo.", nameof(context));

        AccountingLedgerReceiver receiver = new(context);
        OperationCommandInvoker invoker = new();
        IAccountingCommand command = CreateCommand(request, receiver);

        return invoker.Dispatch(command);
    }

    private static IAccountingCommand CreateCommand(AccountingOperationRequest request, AccountingLedgerReceiver receiver)
    {
        string normalizedKind = request.Kind.Trim().ToLowerInvariant();

        return normalizedKind switch
        {
            "apply-payment" or "settle" or "settlement" or "pay" or "payment" or "abono" or "pago" =>
                new SettleBalanceCommand(receiver, request),
            "charge" or "debit" or "cargo" =>
                new IncreaseBalanceCommand(receiver, request),
            "reverse-voucher" or "reverse" or "reversal" or "reversa" or "anular" =>
                new ReverseBalanceCommand(receiver, request),
            _ => throw new ArgumentException($"Tipo de operación no soportado: '{request.Kind}'.", nameof(request))
        };
    }

    // Command interface
    private interface IAccountingCommand
    {
        Guid OperationId { get; }
        AccountingOperationReceipt Execute();
    }

    // Base Command with idempotent replay support
    private abstract class AccountingCommandBase(
        AccountingLedgerReceiver receiver,
        AccountingOperationRequest request) : IAccountingCommand
    {
        protected AccountingLedgerReceiver Receiver { get; } = receiver;
        protected AccountingOperationRequest Request { get; } = request;

        public Guid OperationId => Request.OperationId;

        public AccountingOperationReceipt Execute()
        {
            AccountingOperationReceipt? existing = Receiver.FindCompleted(Request.OperationId);
            if (existing is not null)
                return existing with { Replayed = true };

            return ExecuteCore();
        }

        protected abstract AccountingOperationReceipt ExecuteCore();
    }

    // Concrete Command: Settle / Payment
    private sealed class SettleBalanceCommand(
        AccountingLedgerReceiver receiver,
        AccountingOperationRequest request) : AccountingCommandBase(receiver, request)
    {
        protected override AccountingOperationReceipt ExecuteCore() =>
            Receiver.ApplySettlement(Request);
    }

    // Concrete Command: Charge / Increase
    private sealed class IncreaseBalanceCommand(
        AccountingLedgerReceiver receiver,
        AccountingOperationRequest request) : AccountingCommandBase(receiver, request)
    {
        protected override AccountingOperationReceipt ExecuteCore() =>
            Receiver.ApplyCharge(Request);
    }

    // Concrete Command: Reversal
    private sealed class ReverseBalanceCommand(
        AccountingLedgerReceiver receiver,
        AccountingOperationRequest request) : AccountingCommandBase(receiver, request)
    {
        protected override AccountingOperationReceipt ExecuteCore() =>
            Receiver.ApplyReversal(Request);
    }

    // Receiver: encapsulates state and ledger rules
    private sealed class AccountingLedgerReceiver(AccountingOperationContext context)
    {
        private decimal _outstandingAmount = context.OutstandingAmount;

        public AccountingOperationReceipt? FindCompleted(Guid operationId)
        {
            if (context.CompletedOperations is null)
                return null;

            foreach (AccountingOperationReceipt completed in context.CompletedOperations)
            {
                if (completed is not null && completed.OperationId == operationId)
                    return completed;
            }

            return null;
        }

        public AccountingOperationReceipt ApplySettlement(AccountingOperationRequest request)
        {
            EnsureTargetPublished(request);

            if (request.Amount > _outstandingAmount)
                throw new InvalidOperationException("El monto a abonar supera el saldo pendiente del objetivo.");

            _outstandingAmount -= request.Amount;

            return new AccountingOperationReceipt(
                request.OperationId,
                request.TargetId,
                request.Kind.Trim(),
                request.Amount,
                _outstandingAmount,
                Guid.NewGuid(),
                Replayed: false);
        }

        public AccountingOperationReceipt ApplyCharge(AccountingOperationRequest request)
        {
            EnsureTargetPublished(request);

            _outstandingAmount += request.Amount;

            return new AccountingOperationReceipt(
                request.OperationId,
                request.TargetId,
                request.Kind.Trim(),
                request.Amount,
                _outstandingAmount,
                Guid.NewGuid(),
                Replayed: false);
        }

        public AccountingOperationReceipt ApplyReversal(AccountingOperationRequest request)
        {
            EnsureTargetPublished(request);

            decimal applied = Math.Min(request.Amount, _outstandingAmount);
            _outstandingAmount -= applied;

            Guid linkedEntryId = FindLinkedEntryId(request.TargetId);

            return new AccountingOperationReceipt(
                request.OperationId,
                request.TargetId,
                request.Kind.Trim(),
                applied,
                _outstandingAmount,
                linkedEntryId,
                Replayed: false);
        }

        private Guid FindLinkedEntryId(Guid targetId)
        {
            if (context.CompletedOperations is not null)
            {
                for (int i = context.CompletedOperations.Count - 1; i >= 0; i--)
                {
                    AccountingOperationReceipt completed = context.CompletedOperations[i];
                    if (completed is not null && completed.TargetId == targetId && completed.LinkedJournalEntryId != Guid.Empty)
                        return completed.LinkedJournalEntryId;
                }
            }

            return Guid.NewGuid();
        }

        private void EnsureTargetPublished(AccountingOperationRequest request)
        {
            if (!context.TargetIsPublished)
                throw new InvalidOperationException($"El objetivo '{request.TargetId}' no está publicado para ejecutar operaciones.");
        }
    }

    // Invoker
    private sealed class OperationCommandInvoker
    {
        public AccountingOperationReceipt Dispatch(IAccountingCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            return command.Execute();
        }
    }
}
