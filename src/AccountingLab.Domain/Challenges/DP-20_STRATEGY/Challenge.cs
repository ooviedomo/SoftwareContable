using AccountingLab.Domain.Education;

namespace AccountingLab.Domain.Challenges.DP20Strategy;

public sealed record FictionalTaxRule(string Name, string OperationKind, DateOnly ValidFrom, DateOnly ValidTo, string CalculationKind, decimal Rate, decimal FixedAmount);
public sealed record TaxCalculationRequest(decimal BaseAmount, string OperationKind, DateOnly AccountingDate, IReadOnlyList<FictionalTaxRule> Rules);
public sealed record TaxCalculationResult(decimal BaseAmount, decimal AppliedRate, decimal TaxAmount, string RuleName);
public interface IConfigurableTaxChallenge { TaxCalculationResult Calculate(TaxCalculationRequest request); }
public sealed class ConfigurableTaxChallenge : IConfigurableTaxChallenge
{
    public TaxCalculationResult Calculate(TaxCalculationRequest request)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);

        if (request.BaseAmount < 0m)
            throw new ArgumentException("El monto base no puede ser negativo.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.OperationKind))
            throw new ArgumentException("El tipo de operación es obligatorio.", nameof(request));

        ArgumentNullException.ThrowIfNull(request.Rules);

        FictionalTaxRule? selectedRule = null;

        foreach (FictionalTaxRule rule in request.Rules)
        {
            if (rule is null)
                throw new ArgumentException("La lista de reglas fiscales contiene una regla nula.", nameof(request));

            if (rule.ValidFrom > rule.ValidTo)
                throw new ArgumentException($"La regla '{rule.Name}' tiene un rango de vigencia inválido.", nameof(request));

            bool matchesOperation = string.Equals(
                rule.OperationKind?.Trim(),
                request.OperationKind.Trim(),
                StringComparison.OrdinalIgnoreCase);

            bool isEffective = request.AccountingDate >= rule.ValidFrom &&
                               request.AccountingDate <= rule.ValidTo;

            if (matchesOperation && isEffective)
            {
                selectedRule = rule;
                break;
            }
        }

        if (selectedRule is null)
            throw new InvalidOperationException($"No existe una regla fiscal vigente para la operación '{request.OperationKind}' en la fecha '{request.AccountingDate:yyyy-MM-dd}'.");

        TaxCalculationContext context = new(selectedRule);
        return context.Execute(request.BaseAmount);
    }

    // Strategy interface
    private interface ITaxCalculationStrategy
    {
        TaxCalculationResult Compute(decimal baseAmount, FictionalTaxRule rule);
    }

    // Context
    private sealed class TaxCalculationContext(FictionalTaxRule rule)
    {
        private readonly ITaxCalculationStrategy _strategy = ResolveStrategy(rule.CalculationKind);

        public TaxCalculationResult Execute(decimal baseAmount) =>
            _strategy.Compute(baseAmount, rule);

        private static ITaxCalculationStrategy ResolveStrategy(string? calculationKind)
        {
            string normalized = calculationKind?.Trim().ToLowerInvariant() ?? string.Empty;

            return normalized switch
            {
                "rate" or "percentage" or "percent" or "porcentaje" or "tasa" => new RateTaxStrategy(),
                "fixed" or "flat" or "fijo" or "monto-fijo" or "montofijo" => new FixedAmountTaxStrategy(),
                "compound" or "rate-plus-fixed" or "mixto" or "tasa-mas-fijo" => new CompoundTaxStrategy(),
                _ => throw new ArgumentException($"Tipo de cálculo fiscal no soportado: '{calculationKind}'.", nameof(calculationKind))
            };
        }
    }

    // Concrete Strategy 1: Rate / percentage of base amount
    private sealed class RateTaxStrategy : ITaxCalculationStrategy
    {
        public TaxCalculationResult Compute(decimal baseAmount, FictionalTaxRule rule)
        {
            if (rule.Rate < 0m)
                throw new ArgumentException($"La tasa de la regla '{rule.Name}' no puede ser negativa.");

            decimal normalizedRate = rule.Rate > 1m ? rule.Rate / 100m : rule.Rate;
            decimal taxAmount = RoundCurrency(baseAmount * normalizedRate);
            decimal appliedRate = RoundRate(normalizedRate);

            return new TaxCalculationResult(baseAmount, appliedRate, taxAmount, rule.Name);
        }
    }

    // Concrete Strategy 2: Fixed amount tax
    private sealed class FixedAmountTaxStrategy : ITaxCalculationStrategy
    {
        public TaxCalculationResult Compute(decimal baseAmount, FictionalTaxRule rule)
        {
            if (rule.FixedAmount < 0m)
                throw new ArgumentException($"El monto fijo de la regla '{rule.Name}' no puede ser negativo.");

            decimal taxAmount = RoundCurrency(rule.FixedAmount);
            decimal appliedRate = baseAmount > 0m
                ? RoundRate(taxAmount / baseAmount)
                : 0m;

            return new TaxCalculationResult(baseAmount, appliedRate, taxAmount, rule.Name);
        }
    }

    // Concrete Strategy 3: Percentage rate plus fixed amount
    private sealed class CompoundTaxStrategy : ITaxCalculationStrategy
    {
        public TaxCalculationResult Compute(decimal baseAmount, FictionalTaxRule rule)
        {
            if (rule.Rate < 0m || rule.FixedAmount < 0m)
                throw new ArgumentException($"Los parámetros de la regla mixta '{rule.Name}' no pueden ser negativos.");

            decimal normalizedRate = rule.Rate > 1m ? rule.Rate / 100m : rule.Rate;
            decimal taxAmount = RoundCurrency((baseAmount * normalizedRate) + rule.FixedAmount);
            decimal appliedRate = baseAmount > 0m
                ? RoundRate(taxAmount / baseAmount)
                : RoundRate(normalizedRate);

            return new TaxCalculationResult(baseAmount, appliedRate, taxAmount, rule.Name);
        }
    }

    private static decimal RoundCurrency(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private static decimal RoundRate(decimal rate) =>
        decimal.Round(rate, 4, MidpointRounding.AwayFromZero);
}