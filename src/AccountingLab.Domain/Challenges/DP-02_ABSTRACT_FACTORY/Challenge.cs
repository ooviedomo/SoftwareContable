namespace AccountingLab.Domain.Challenges.DP02AbstractFactory;

public sealed record AccountingProfileRequest(string Profile, string FictionalRegime);
public sealed record AccountingFamilyMember(string Role, string CompatibilityKey);
public sealed record AccountingServiceFamily(string Profile, string FictionalRegime, IReadOnlyList<AccountingFamilyMember> Members);
public interface IAccountingProfileChallenge { AccountingServiceFamily CreateFamily(AccountingProfileRequest request); }

public sealed class AccountingProfileChallenge : IAccountingProfileChallenge
{
    public AccountingServiceFamily CreateFamily(AccountingProfileRequest request)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Profile))
            throw new ArgumentException("El perfil contable es obligatorio.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.FictionalRegime))
            throw new ArgumentException("El régimen ficticio es obligatorio.", nameof(request));

        string normalizedProfile = request.Profile.Trim();
        string normalizedRegime = request.FictionalRegime.Trim();

        IAccountingServiceFactory factory = ResolveFactory(normalizedProfile);
        string compatibilityKey = $"{normalizedProfile.ToLowerInvariant()}::{normalizedRegime}";

        IReadOnlyList<AccountingFamilyMember> members =
        [
            factory.CreateCalculationService(compatibilityKey).BuildMember(),
            factory.CreateValidationService(compatibilityKey).BuildMember(),
            factory.CreateReportService(compatibilityKey).BuildMember()
        ];

        return new AccountingServiceFamily(normalizedProfile, normalizedRegime, members);
    }

    private static IAccountingServiceFactory ResolveFactory(string profile) =>
        profile.ToLowerInvariant() switch
        {
            "comercial" => new CommercialAccountingFactory(),
            "servicios" => new ServicesAccountingFactory(),
            _ => throw new ArgumentException($"Perfil contable no soportado: '{profile}'.", nameof(profile))
        };

    // Abstract Factory
    private interface IAccountingServiceFactory
    {
        ICalculationRoleService CreateCalculationService(string compatibilityKey);
        IValidationRoleService CreateValidationService(string compatibilityKey);
        IReportRoleService CreateReportService(string compatibilityKey);
    }

    // Abstract Products
    private interface ICalculationRoleService
    {
        AccountingFamilyMember BuildMember();
    }

    private interface IValidationRoleService
    {
        AccountingFamilyMember BuildMember();
    }

    private interface IReportRoleService
    {
        AccountingFamilyMember BuildMember();
    }

    // Concrete Factory 1: Comercial
    private sealed class CommercialAccountingFactory : IAccountingServiceFactory
    {
        public ICalculationRoleService CreateCalculationService(string compatibilityKey) =>
            new CommercialCalculationService(compatibilityKey);

        public IValidationRoleService CreateValidationService(string compatibilityKey) =>
            new CommercialValidationService(compatibilityKey);

        public IReportRoleService CreateReportService(string compatibilityKey) =>
            new CommercialReportService(compatibilityKey);
    }

    // Concrete Factory 2: Servicios
    private sealed class ServicesAccountingFactory : IAccountingServiceFactory
    {
        public ICalculationRoleService CreateCalculationService(string compatibilityKey) =>
            new ServicesCalculationService(compatibilityKey);

        public IValidationRoleService CreateValidationService(string compatibilityKey) =>
            new ServicesValidationService(compatibilityKey);

        public IReportRoleService CreateReportService(string compatibilityKey) =>
            new ServicesReportService(compatibilityKey);
    }

    // Concrete Products - Comercial
    private sealed class CommercialCalculationService(string compatibilityKey) : ICalculationRoleService
    {
        public AccountingFamilyMember BuildMember() => new("calculo", compatibilityKey);
    }

    private sealed class CommercialValidationService(string compatibilityKey) : IValidationRoleService
    {
        public AccountingFamilyMember BuildMember() => new("validacion", compatibilityKey);
    }

    private sealed class CommercialReportService(string compatibilityKey) : IReportRoleService
    {
        public AccountingFamilyMember BuildMember() => new("reporte", compatibilityKey);
    }

    // Concrete Products - Servicios
    private sealed class ServicesCalculationService(string compatibilityKey) : ICalculationRoleService
    {
        public AccountingFamilyMember BuildMember() => new("calculo", compatibilityKey);
    }

    private sealed class ServicesValidationService(string compatibilityKey) : IValidationRoleService
    {
        public AccountingFamilyMember BuildMember() => new("validacion", compatibilityKey);
    }

    private sealed class ServicesReportService(string compatibilityKey) : IReportRoleService
    {
        public AccountingFamilyMember BuildMember() => new("reporte", compatibilityKey);
    }
}