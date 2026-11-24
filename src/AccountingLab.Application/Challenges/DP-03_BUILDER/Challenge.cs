using AccountingLab.Domain.Education;

namespace AccountingLab.Application.Challenges.DP03Builder;

public sealed record FinancialReportRequest(string Title, DateOnly From, DateOnly To, IReadOnlyList<string> Sections, IReadOnlyList<string> Annexes);
public sealed record FinancialReport(string Title, DateOnly From, DateOnly To, IReadOnlyList<string> Sections, IReadOnlyList<string> Annexes);
public interface IReportAssemblyChallenge { FinancialReport Build(FinancialReportRequest request); }
public sealed class ReportAssemblyChallenge : IReportAssemblyChallenge
{
    public FinancialReport Build(FinancialReportRequest request)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("El título del reporte es obligatorio.", nameof(request));

        if (request.To < request.From)
            throw new ArgumentException("La fecha final ('To') no puede ser anterior a la fecha inicial ('From').", nameof(request));

        IReportBuilder builder = new FinancialReportBuilder()
            .SetTitle(request.Title)
            .SetPeriod(request.From, request.To)
            .AddSections(request.Sections)
            .AddAnnexes(request.Annexes);

        return new ReportDirector().Construct(builder);
    }

    private interface IReportBuilder
    {
        IReportBuilder SetTitle(string title);
        IReportBuilder SetPeriod(DateOnly from, DateOnly to);
        IReportBuilder AddSections(IReadOnlyList<string>? sections);
        IReportBuilder AddAnnexes(IReadOnlyList<string>? annexes);
        FinancialReport Build();
    }

    private sealed class FinancialReportBuilder : IReportBuilder
    {
        private string _title = string.Empty;
        private DateOnly _from;
        private DateOnly _to;
        private readonly List<string> _sections = [];
        private readonly List<string> _annexes = [];

        public IReportBuilder SetTitle(string title)
        {
            _title = title.Trim();
            return this;
        }

        public IReportBuilder SetPeriod(DateOnly from, DateOnly to)
        {
            _from = from;
            _to = to;
            return this;
        }

        public IReportBuilder AddSections(IReadOnlyList<string>? sections)
        {
            if (sections is not null)
            {
                foreach (string section in sections)
                {
                    if (!string.IsNullOrWhiteSpace(section))
                        _sections.Add(section.Trim());
                }
            }

            return this;
        }

        public IReportBuilder AddAnnexes(IReadOnlyList<string>? annexes)
        {
            if (annexes is not null)
            {
                foreach (string annex in annexes)
                {
                    if (!string.IsNullOrWhiteSpace(annex))
                        _annexes.Add(annex.Trim());
                }
            }

            return this;
        }

        public FinancialReport Build() =>
            new(
                _title,
                _from,
                _to,
                _sections.AsReadOnly(),
                _annexes.AsReadOnly()
            );
    }

    private sealed class ReportDirector
    {
        public FinancialReport Construct(IReportBuilder builder) => builder.Build();
    }
}

