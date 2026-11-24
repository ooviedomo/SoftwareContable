using AccountingLab.Domain.Education;

namespace AccountingLab.Application.Challenges.DP21TemplateMethod;

public sealed record BankStatementItem(string Id, string Reference, DateOnly Date, decimal Amount);
public sealed record ReconciliationLedgerItem(string Id, string Reference, DateOnly Date, decimal Amount);
public sealed record BankReconciliationRequest(string Mode, IReadOnlyList<BankStatementItem> BankItems, IReadOnlyList<ReconciliationLedgerItem> LedgerItems, decimal Tolerance);
public sealed record ReconciliationMatch(string BankItemId, string LedgerItemId, decimal Difference);
public sealed record BankReconciliationResult(IReadOnlyList<ReconciliationMatch> Matches, IReadOnlyList<string> UnmatchedBankItems, IReadOnlyList<string> UnmatchedLedgerItems);
public interface IBankReconciliationChallenge { BankReconciliationResult Reconcile(BankReconciliationRequest request); }
public sealed class BankReconciliationChallenge : IBankReconciliationChallenge
{
    public BankReconciliationResult Reconcile(BankReconciliationRequest request)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Mode))
            throw new ArgumentException("El modo de conciliación es obligatorio.", nameof(request));

        ArgumentNullException.ThrowIfNull(request.BankItems);
        ArgumentNullException.ThrowIfNull(request.LedgerItems);

        if (request.Tolerance < 0m)
            throw new ArgumentOutOfRangeException(nameof(request), "La tolerancia no puede ser negativa.");

        ReconciliationTemplateProcessor processor = ResolveProcessor(request.Mode);
        return processor.Execute(request);
    }

    private static ReconciliationTemplateProcessor ResolveProcessor(string mode) =>
        mode.Trim().ToLowerInvariant() switch
        {
            "exact" or "estricto" or "strict" => new ExactReconciliationProcessor(),
            "reference" or "referencia" or "ref" => new ReferenceReconciliationProcessor(),
            "tolerance" or "tolerancia" or "fuzzy" or "window" => new ToleranceReconciliationProcessor(),
            _ => throw new ArgumentException($"Modo de conciliación no soportado: '{mode}'.", nameof(mode))
        };

    // AbstractClass: defines the invariant reconciliation skeleton and hook/step primitives
    private abstract class ReconciliationTemplateProcessor
    {
        public BankReconciliationResult Execute(BankReconciliationRequest request)
        {
            ValidateItems(request);

            List<BankStatementItem> orderedBank = OrderBankItems(request.BankItems);
            List<ReconciliationLedgerItem> orderedLedger = OrderLedgerItems(request.LedgerItems);

            HashSet<string> usedLedgerIds = [];
            List<ReconciliationMatch> matches = [];
            List<string> unmatchedBankIds = [];

            foreach (BankStatementItem bankItem in orderedBank)
            {
                ReconciliationLedgerItem? matchedLedger = FindBestMatch(bankItem, orderedLedger, usedLedgerIds, request);

                if (matchedLedger is not null)
                {
                    usedLedgerIds.Add(matchedLedger.Id);
                    decimal difference = RoundAmount(CalculateDifference(bankItem, matchedLedger));
                    matches.Add(new ReconciliationMatch(bankItem.Id, matchedLedger.Id, difference));
                }
                else
                {
                    unmatchedBankIds.Add(bankItem.Id);
                }
            }

            List<string> unmatchedLedgerIds = [];
            foreach (ReconciliationLedgerItem ledgerItem in orderedLedger)
            {
                if (!usedLedgerIds.Contains(ledgerItem.Id))
                    unmatchedLedgerIds.Add(ledgerItem.Id);
            }

            return new BankReconciliationResult(
                matches.AsReadOnly(),
                unmatchedBankIds.AsReadOnly(),
                unmatchedLedgerIds.AsReadOnly());
        }

        protected virtual void ValidateItems(BankReconciliationRequest request)
        {
            foreach (BankStatementItem bankItem in request.BankItems)
            {
                if (bankItem is null)
                    throw new ArgumentException("La lista de movimientos bancarios contiene un elemento nulo.", nameof(request));
            }

            foreach (ReconciliationLedgerItem ledgerItem in request.LedgerItems)
            {
                if (ledgerItem is null)
                    throw new ArgumentException("La lista de movimientos contables contiene un elemento nulo.", nameof(request));
            }
        }

        protected virtual List<BankStatementItem> OrderBankItems(IReadOnlyList<BankStatementItem> bankItems) =>
            [.. bankItems];

        protected virtual List<ReconciliationLedgerItem> OrderLedgerItems(IReadOnlyList<ReconciliationLedgerItem> ledgerItems) =>
            [.. ledgerItems];

        protected abstract ReconciliationLedgerItem? FindBestMatch(
            BankStatementItem bankItem,
            IReadOnlyList<ReconciliationLedgerItem> ledgerItems,
            HashSet<string> usedLedgerIds,
            BankReconciliationRequest request);

        protected virtual decimal CalculateDifference(BankStatementItem bankItem, ReconciliationLedgerItem ledgerItem) =>
            bankItem.Amount - ledgerItem.Amount;

        protected static decimal RoundAmount(decimal value) =>
            decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    }

    // ConcreteClass 1: Exact match on amount, date, and reference
    private sealed class ExactReconciliationProcessor : ReconciliationTemplateProcessor
    {
        protected override ReconciliationLedgerItem? FindBestMatch(
            BankStatementItem bankItem,
            IReadOnlyList<ReconciliationLedgerItem> ledgerItems,
            HashSet<string> usedLedgerIds,
            BankReconciliationRequest request)
        {
            foreach (ReconciliationLedgerItem ledgerItem in ledgerItems)
            {
                if (usedLedgerIds.Contains(ledgerItem.Id))
                    continue;

                bool sameAmount = ledgerItem.Amount == bankItem.Amount;
                bool sameDate = ledgerItem.Date == bankItem.Date;
                bool sameReference = string.Equals(
                    ledgerItem.Reference?.Trim(),
                    bankItem.Reference?.Trim(),
                    StringComparison.OrdinalIgnoreCase);

                if (sameAmount && sameDate && sameReference)
                    return ledgerItem;
            }

            return null;
        }
    }

    // ConcreteClass 2: Match by normalized reference and amount within tolerance
    private sealed class ReferenceReconciliationProcessor : ReconciliationTemplateProcessor
    {
        protected override ReconciliationLedgerItem? FindBestMatch(
            BankStatementItem bankItem,
            IReadOnlyList<ReconciliationLedgerItem> ledgerItems,
            HashSet<string> usedLedgerIds,
            BankReconciliationRequest request)
        {
            if (string.IsNullOrWhiteSpace(bankItem.Reference))
                return null;

            ReconciliationLedgerItem? bestMatch = null;
            decimal smallestDiff = decimal.MaxValue;

            foreach (ReconciliationLedgerItem ledgerItem in ledgerItems)
            {
                if (usedLedgerIds.Contains(ledgerItem.Id))
                    continue;

                bool sameReference = string.Equals(
                    ledgerItem.Reference?.Trim(),
                    bankItem.Reference.Trim(),
                    StringComparison.OrdinalIgnoreCase);

                if (!sameReference)
                    continue;

                decimal absDiff = Math.Abs(bankItem.Amount - ledgerItem.Amount);
                if (absDiff <= request.Tolerance && absDiff < smallestDiff)
                {
                    smallestDiff = absDiff;
                    bestMatch = ledgerItem;
                }
            }

            return bestMatch;
        }
    }

    // ConcreteClass 3: Match within amount tolerance and date proximity
    private sealed class ToleranceReconciliationProcessor : ReconciliationTemplateProcessor
    {
        protected override ReconciliationLedgerItem? FindBestMatch(
            BankStatementItem bankItem,
            IReadOnlyList<ReconciliationLedgerItem> ledgerItems,
            HashSet<string> usedLedgerIds,
            BankReconciliationRequest request)
        {
            ReconciliationLedgerItem? bestMatch = null;
            (decimal AmountDiff, int DayDiff) bestScore = (decimal.MaxValue, int.MaxValue);

            foreach (ReconciliationLedgerItem ledgerItem in ledgerItems)
            {
                if (usedLedgerIds.Contains(ledgerItem.Id))
                    continue;

                decimal amountDiff = Math.Abs(bankItem.Amount - ledgerItem.Amount);
                if (amountDiff > request.Tolerance)
                    continue;

                int dayDiff = Math.Abs(bankItem.Date.DayNumber - ledgerItem.Date.DayNumber);
                if (dayDiff > 5)
                    continue;

                (decimal AmountDiff, int DayDiff) candidateScore = (amountDiff, dayDiff);
                if (candidateScore.CompareTo(bestScore) < 0)
                {
                    bestScore = candidateScore;
                    bestMatch = ledgerItem;
                }
            }

            return bestMatch;
        }
    }
}