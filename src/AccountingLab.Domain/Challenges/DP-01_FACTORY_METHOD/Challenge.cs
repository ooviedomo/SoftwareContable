namespace AccountingLab.Domain.Challenges.DP01FactoryMethod;

public sealed record VoucherCreationRequest(Guid CompanyId, string Kind, decimal Amount, string Currency);
public sealed record VoucherDraft(Guid Id, Guid CompanyId, string Kind, decimal Amount, string Currency);
public interface IVoucherCreationChallenge { VoucherDraft Create(VoucherCreationRequest request); }

public sealed class VoucherCreationChallenge : IVoucherCreationChallenge
{
    private static readonly IReadOnlyDictionary<string, Func<VoucherCreator>> CreatorRegistry =
        new Dictionary<string, Func<VoucherCreator>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ingreso"] = static () => new IncomeVoucherCreator(),
            ["egreso"] = static () => new ExpenseVoucherCreator(),
            ["ajuste"] = static () => new AdjustmentVoucherCreator(),
            ["traslado"] = static () => new TransferVoucherCreator()
        };

    public VoucherDraft Create(VoucherCreationRequest request)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "El importe del comprobante no puede ser negativo.");

        if (string.IsNullOrWhiteSpace(request.Kind) ||
            !CreatorRegistry.TryGetValue(request.Kind.Trim(), out Func<VoucherCreator>? creatorFactory))
        {
            throw new ArgumentException($"Tipo de comprobante no soportado: '{request.Kind}'.", nameof(request));
        }

        VoucherCreator creator = creatorFactory();
        return creator.CreateDraft(request);
    }

    private abstract class VoucherCreator
    {
        // Factory Method
        protected abstract VoucherDraft BuildDraft(VoucherCreationRequest request);

        public VoucherDraft CreateDraft(VoucherCreationRequest request) => BuildDraft(request);
    }

    private sealed class IncomeVoucherCreator : VoucherCreator
    {
        protected override VoucherDraft BuildDraft(VoucherCreationRequest request) =>
            new(Guid.NewGuid(), request.CompanyId, request.Kind, request.Amount, request.Currency);
    }

    private sealed class ExpenseVoucherCreator : VoucherCreator
    {
        protected override VoucherDraft BuildDraft(VoucherCreationRequest request) =>
            new(Guid.NewGuid(), request.CompanyId, request.Kind, request.Amount, request.Currency);
    }

    private sealed class AdjustmentVoucherCreator : VoucherCreator
    {
        protected override VoucherDraft BuildDraft(VoucherCreationRequest request) =>
            new(Guid.NewGuid(), request.CompanyId, request.Kind, request.Amount, request.Currency);
    }

    private sealed class TransferVoucherCreator : VoucherCreator
    {
        protected override VoucherDraft BuildDraft(VoucherCreationRequest request) =>
            new(Guid.NewGuid(), request.CompanyId, request.Kind, request.Amount, request.Currency);
    }
}