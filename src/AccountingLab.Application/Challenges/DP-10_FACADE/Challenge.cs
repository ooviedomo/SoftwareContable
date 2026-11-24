using AccountingLab.Domain.Education;

namespace AccountingLab.Application.Challenges.DP10Facade;

public sealed record ClosingAccountBalance(string AccountCode, decimal Debit, decimal Credit);
public sealed record ClosePeriodRequest(Guid CompanyId, DateOnly From, DateOnly To, IReadOnlyList<ClosingAccountBalance> Balances, int UnpostedDocuments);
public sealed record PeriodClosingSummary(Guid CompanyId, DateOnly From, DateOnly To, bool Closed, decimal TotalDebit, decimal TotalCredit);
public interface IPeriodClosingChallenge { PeriodClosingSummary Close(ClosePeriodRequest request); }
public sealed class PeriodClosingChallenge : IPeriodClosingChallenge
{
    public PeriodClosingSummary Close(ClosePeriodRequest request)
    {
        // Solucionado
        PeriodClosingFacade facade = new(
            new PeriodValidationSubsystem(),
            new LedgerTotalsSubsystem(),
            new PeriodLockSubsystem());

        return facade.ClosePeriod(request);
    }

    // Facade
    private sealed class PeriodClosingFacade(
        PeriodValidationSubsystem validator,
        LedgerTotalsSubsystem totalsCalculator,
        PeriodLockSubsystem periodLocker)
    {
        public PeriodClosingSummary ClosePeriod(ClosePeriodRequest request)
        {
            validator.Validate(request);

            (decimal totalDebit, decimal totalCredit) = totalsCalculator.CalculateTotals(request.Balances);
            bool closed = periodLocker.EnsureCanCloseAndLock(request, totalDebit, totalCredit);

            return new PeriodClosingSummary(
                request.CompanyId,
                request.From,
                request.To,
                closed,
                totalDebit,
                totalCredit);
        }
    }

    // Subsystem 1: Validation
    private sealed class PeriodValidationSubsystem
    {
        public void Validate(ClosePeriodRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.CompanyId == Guid.Empty)
                throw new ArgumentException("El identificador de empresa es obligatorio.", nameof(request));

            if (request.To < request.From)
                throw new ArgumentException("La fecha final ('To') no puede ser anterior a la fecha inicial ('From').", nameof(request));

            if (request.UnpostedDocuments < 0)
                throw new ArgumentException("La cantidad de documentos sin contabilizar no puede ser negativa.", nameof(request));

            ArgumentNullException.ThrowIfNull(request.Balances);

            foreach (ClosingAccountBalance balance in request.Balances)
            {
                if (balance is null)
                    throw new ArgumentException("La lista de saldos contiene un registro nulo.", nameof(request));

                if (balance.Debit < 0 || balance.Credit < 0)
                    throw new ArgumentException($"Los débitos y créditos de la cuenta '{balance.AccountCode}' no pueden ser negativos.", nameof(request));
            }
        }
    }

    // Subsystem 2: Ledger Totals
    private sealed class LedgerTotalsSubsystem
    {
        public (decimal TotalDebit, decimal TotalCredit) CalculateTotals(IReadOnlyList<ClosingAccountBalance> balances)
        {
            decimal totalDebit = 0m;
            decimal totalCredit = 0m;

            foreach (ClosingAccountBalance balance in balances)
            {
                totalDebit += balance.Debit;
                totalCredit += balance.Credit;
            }

            return (totalDebit, totalCredit);
        }
    }

    // Subsystem 3: Period Closing Eligibility / Lock
    private sealed class PeriodLockSubsystem
    {
        public bool EnsureCanCloseAndLock(ClosePeriodRequest request, decimal totalDebit, decimal totalCredit)
        {
            if (request.UnpostedDocuments > 0)
                throw new InvalidOperationException($"No se puede cerrar el período porque existen {request.UnpostedDocuments} documento(s) sin contabilizar.");

            if (totalDebit != totalCredit)
                throw new InvalidOperationException($"No se puede cerrar el período porque existe un descuadre contable (Débito: {totalDebit}, Crédito: {totalCredit}).");

            return true;
        }
    }
}