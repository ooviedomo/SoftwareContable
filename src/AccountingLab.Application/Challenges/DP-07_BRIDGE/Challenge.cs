using System.Globalization;
using AccountingLab.Domain.Education;

namespace AccountingLab.Application.Challenges.DP07Bridge;

public sealed record ReportPresentationRequest(string ReportType, string Format, decimal Amount);
public sealed record RenderedFinancialReport(string ReportType, string Format, string ContentType, string Content);
public interface IFinancialRepresentationChallenge { RenderedFinancialReport Render(ReportPresentationRequest request); }
public sealed class FinancialRepresentationChallenge : IFinancialRepresentationChallenge
{
    public RenderedFinancialReport Render(ReportPresentationRequest request)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ReportType))
            throw new ArgumentException("El tipo de reporte es obligatorio.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Format))
            throw new ArgumentException("El formato de salida es obligatorio.", nameof(request));

        string normalizedReportType = request.ReportType.Trim();
        string normalizedFormat = request.Format.Trim();

        IReportFormatter formatter = ResolveFormatter(normalizedFormat);
        FinancialReportAbstraction report = ResolveReportAbstraction(normalizedReportType, formatter);

        return report.Render(normalizedReportType, normalizedFormat, request.Amount);
    }

    private static string FormatExactAmount(decimal amount) =>
        amount.ToString("0.0000", CultureInfo.InvariantCulture);

    private static IReportFormatter ResolveFormatter(string format) =>
        format.ToLowerInvariant() switch
        {
            "json" => new JsonReportFormatter(),
            "csv" => new CsvReportFormatter(),
            "html" => new HtmlReportFormatter(),
            "xml" => new XmlReportFormatter(),
            "txt" or "texto" or "plain" => new PlainTextReportFormatter(),
            "md" or "markdown" => new MarkdownReportFormatter(),
            _ => throw new ArgumentException($"Formato de reporte no soportado: '{format}'.", nameof(format))
        };

    private static FinancialReportAbstraction ResolveReportAbstraction(string reportType, IReportFormatter formatter) =>
        reportType.ToLowerInvariant() switch
        {
            "balance" or "balance-general" or "balance_general" => new BalanceReportAbstraction(formatter),
            "resultados" or "estado-resultados" or "estado_resultados" => new IncomeStatementReportAbstraction(formatter),
            "flujo" or "flujo-caja" or "flujo_caja" => new CashFlowReportAbstraction(formatter),
            "resumen" or "ejecutivo" => new SummaryReportAbstraction(formatter),
            "detalle" or "detallado" => new DetailedReportAbstraction(formatter),
            _ => throw new ArgumentException($"Tipo de reporte no soportado: '{reportType}'.", nameof(reportType))
        };

    // Implementor
    private interface IReportFormatter
    {
        string ContentType { get; }
        string FormatDocument(string reportType, string heading, decimal amount, IReadOnlyDictionary<string, string> sections);
    }

    // Concrete Implementors
    private sealed class JsonReportFormatter : IReportFormatter
    {
        public string ContentType => "application/json";

        public string FormatDocument(string reportType, string heading, decimal amount, IReadOnlyDictionary<string, string> sections)
        {
            string formattedAmount = FormatExactAmount(amount);
            string extraFields = sections.Count > 0
                ? ", " + string.Join(", ", sections.Select(kv => $"\"{kv.Key}\": \"{kv.Value}\""))
                : string.Empty;
            return $"{{\"reportType\": \"{reportType}\", \"heading\": \"{heading}\", \"amount\": \"{formattedAmount}\"{extraFields}}}";
        }
    }

    private sealed class CsvReportFormatter : IReportFormatter
    {
        public string ContentType => "text/csv";

        public string FormatDocument(string reportType, string heading, decimal amount, IReadOnlyDictionary<string, string> sections)
        {
            string formattedAmount = FormatExactAmount(amount);
            string headers = "reportType,heading,amount" + (sections.Count > 0 ? "," + string.Join(",", sections.Keys) : string.Empty);
            string values = $"{reportType},{heading},{formattedAmount}" + (sections.Count > 0 ? "," + string.Join(",", sections.Values) : string.Empty);
            return $"{headers}\n{values}";
        }
    }

    private sealed class HtmlReportFormatter : IReportFormatter
    {
        public string ContentType => "text/html";

        public string FormatDocument(string reportType, string heading, decimal amount, IReadOnlyDictionary<string, string> sections)
        {
            string formattedAmount = FormatExactAmount(amount);
            string items = string.Join("", sections.Select(kv => $"<li>{kv.Key}: {kv.Value}</li>"));
            return $"<article data-report=\"{reportType}\"><h1>{heading}</h1><p>Amount: {formattedAmount}</p><ul>{items}</ul></article>";
        }
    }

    private sealed class XmlReportFormatter : IReportFormatter
    {
        public string ContentType => "application/xml";

        public string FormatDocument(string reportType, string heading, decimal amount, IReadOnlyDictionary<string, string> sections)
        {
            string formattedAmount = FormatExactAmount(amount);
            string nodes = string.Join("", sections.Select(kv => $"<{kv.Key}>{kv.Value}</{kv.Key}>"));
            return $"<report type=\"{reportType}\"><heading>{heading}</heading><amount>{formattedAmount}</amount>{nodes}</report>";
        }
    }

    private sealed class PlainTextReportFormatter : IReportFormatter
    {
        public string ContentType => "text/plain";

        public string FormatDocument(string reportType, string heading, decimal amount, IReadOnlyDictionary<string, string> sections)
        {
            string formattedAmount = FormatExactAmount(amount);
            string details = sections.Count > 0
                ? " | " + string.Join(" | ", sections.Select(kv => $"{kv.Key}={kv.Value}"))
                : string.Empty;
            return $"[{reportType}] {heading} | Amount={formattedAmount}{details}";
        }
    }

    private sealed class MarkdownReportFormatter : IReportFormatter
    {
        public string ContentType => "text/markdown";

        public string FormatDocument(string reportType, string heading, decimal amount, IReadOnlyDictionary<string, string> sections)
        {
            string formattedAmount = FormatExactAmount(amount);
            string bullets = sections.Count > 0
                ? "\n" + string.Join("\n", sections.Select(kv => $"- **{kv.Key}:** {kv.Value}"))
                : string.Empty;
            return $"# {heading} ({reportType})\n- **Amount:** {formattedAmount}{bullets}";
        }
    }

    // Abstraction (Bridge)
    private abstract class FinancialReportAbstraction(IReportFormatter formatter)
    {
        protected IReportFormatter Formatter { get; } = formatter;

        protected abstract string Heading { get; }

        protected abstract IReadOnlyDictionary<string, string> BuildSections(decimal amount);

        public RenderedFinancialReport Render(string reportType, string format, decimal amount)
        {
            IReadOnlyDictionary<string, string> sections = BuildSections(amount);
            string content = Formatter.FormatDocument(reportType, Heading, amount, sections);
            return new RenderedFinancialReport(reportType, format, Formatter.ContentType, content);
        }
    }

    // Refined Abstractions
    private sealed class BalanceReportAbstraction(IReportFormatter formatter) : FinancialReportAbstraction(formatter)
    {
        protected override string Heading => "Balance General";

        protected override IReadOnlyDictionary<string, string> BuildSections(decimal amount) =>
            new Dictionary<string, string>
            {
                ["scope"] = "situacion_patrimonial",
                ["netBalance"] = FormatExactAmount(amount)
            };
    }

    private sealed class IncomeStatementReportAbstraction(IReportFormatter formatter) : FinancialReportAbstraction(formatter)
    {
        protected override string Heading => "Estado de Resultados";

        protected override IReadOnlyDictionary<string, string> BuildSections(decimal amount) =>
            new Dictionary<string, string>
            {
                ["scope"] = "ingresos_y_gastos",
                ["operatingResult"] = FormatExactAmount(amount)
            };
    }

    private sealed class CashFlowReportAbstraction(IReportFormatter formatter) : FinancialReportAbstraction(formatter)
    {
        protected override string Heading => "Flujo de Caja";

        protected override IReadOnlyDictionary<string, string> BuildSections(decimal amount) =>
            new Dictionary<string, string>
            {
                ["scope"] = "liquidez_operativa",
                ["netCashFlow"] = FormatExactAmount(amount)
            };
    }

    private sealed class SummaryReportAbstraction(IReportFormatter formatter) : FinancialReportAbstraction(formatter)
    {
        protected override string Heading => "Reporte Resumen";

        protected override IReadOnlyDictionary<string, string> BuildSections(decimal amount) =>
            new Dictionary<string, string>
            {
                ["scope"] = "resumen_ejecutivo",
                ["total"] = FormatExactAmount(amount)
            };
    }

    private sealed class DetailedReportAbstraction(IReportFormatter formatter) : FinancialReportAbstraction(formatter)
    {
        protected override string Heading => "Reporte Detallado";

        protected override IReadOnlyDictionary<string, string> BuildSections(decimal amount) =>
            new Dictionary<string, string>
            {
                ["scope"] = "desglose_contable",
                ["detailedTotal"] = FormatExactAmount(amount)
            };
    }
}