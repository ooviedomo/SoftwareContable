using AccountingLab.Domain.Education;

namespace AccountingLab.Application.Challenges.DP12Proxy;

public sealed record SensitiveReportRequest(Guid CompanyId, bool MayRead, string ReportKey);
public sealed record SensitiveFinancialReport(Guid CompanyId, string ReportKey, decimal ConfidentialAmount);
public interface ISensitiveReportChallenge { SensitiveFinancialReport Get(SensitiveReportRequest request, Func<SensitiveFinancialReport> loadReport); }
public sealed class SensitiveReportChallenge : ISensitiveReportChallenge
{
    public SensitiveFinancialReport Get(SensitiveReportRequest request, Func<SensitiveFinancialReport> loadReport)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(loadReport);

        if (request.CompanyId == Guid.Empty)
            throw new ArgumentException("El identificador de empresa es obligatorio.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ReportKey))
            throw new ArgumentException("La clave del reporte es obligatoria.", nameof(request));

        ISensitiveReportReader realSubject = new DelegateSensitiveReportReader(loadReport);
        ISensitiveReportReader proxy = new SensitiveReportAccessProxy(realSubject, request);

        return proxy.GetReport();
    }

    // Subject
    private interface ISensitiveReportReader
    {
        SensitiveFinancialReport GetReport();
    }

    // RealSubject
    private sealed class DelegateSensitiveReportReader(Func<SensitiveFinancialReport> loader) : ISensitiveReportReader
    {
        public SensitiveFinancialReport GetReport()
        {
            return loader()
                ?? throw new InvalidOperationException("El cargador del reporte sensible devolvió un resultado nulo.");
        }
    }

    // Protection Proxy
    private sealed class SensitiveReportAccessProxy(
        ISensitiveReportReader realSubject,
        SensitiveReportRequest request) : ISensitiveReportReader
    {
        public SensitiveFinancialReport GetReport()
        {
            if (!request.MayRead)
                throw new UnauthorizedAccessException(
                    $"Acceso denegado al reporte sensible '{request.ReportKey}' para la empresa '{request.CompanyId}'.");

            SensitiveFinancialReport report = realSubject.GetReport();

            if (report.CompanyId != request.CompanyId)
                throw new InvalidOperationException(
                    $"El reporte '{report.ReportKey}' pertenece a la empresa '{report.CompanyId}' y no coincide con la empresa solicitante '{request.CompanyId}'.");

            return report;
        }
    }
}