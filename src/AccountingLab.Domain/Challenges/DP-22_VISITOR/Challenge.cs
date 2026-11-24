using AccountingLab.Domain.Education;

namespace AccountingLab.Domain.Challenges.DP22Visitor;

public interface IAccountingAnalysisDocument { Guid Id { get; } }
public sealed record InvoiceAnalysisDocument(Guid Id, decimal NetAmount, decimal TaxAmount) : IAccountingAnalysisDocument;
public sealed record PaymentAnalysisDocument(Guid Id, decimal Amount) : IAccountingAnalysisDocument;
public sealed record AnalysisPostingLine(decimal Debit, decimal Credit);
public sealed record JournalAnalysisDocument(Guid Id, IReadOnlyList<AnalysisPostingLine> Lines) : IAccountingAnalysisDocument;
public sealed record DocumentAnalysisRequest(string View, IReadOnlyList<IAccountingAnalysisDocument> Documents);
public sealed record DocumentAnalysisResult(decimal TotalAmount, int DocumentCount, IReadOnlyDictionary<string, decimal> ByKind);
public interface IDocumentAnalysisChallenge { DocumentAnalysisResult Analyze(DocumentAnalysisRequest request); }
public sealed class DocumentAnalysisChallenge : IDocumentAnalysisChallenge
{
    public DocumentAnalysisResult Analyze(DocumentAnalysisRequest request)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.View))
            throw new ArgumentException("La vista de análisis es obligatoria.", nameof(request));

        ArgumentNullException.ThrowIfNull(request.Documents);

        IAccountingDocumentVisitor visitor = ResolveVisitor(request.View);
        DocumentAnalysisAggregator aggregator = new(visitor);

        foreach (IAccountingAnalysisDocument document in request.Documents)
        {
            if (document is null)
                throw new ArgumentException("La lista de documentos contiene un elemento nulo.", nameof(request));

            aggregator.Visit(document);
        }

        return aggregator.BuildResult();
    }

    private static IAccountingDocumentVisitor ResolveVisitor(string view) =>
        view.Trim().ToLowerInvariant() switch
        {
            "total" or "gross" or "importe" or "bruto" => new GrossAmountVisitor(),
            "count" or "cantidad" or "cantidades" or "conteo" => new CountDocumentVisitor(),
            "tax" or "impuesto" or "impuestos" or "fiscal" => new TaxExposureVisitor(),
            "cash" or "liquidity" or "efectivo" or "caja" => new CashImpactVisitor(),
            "ledger" or "accounting" or "contable" or "asientos" => new LedgerVolumeVisitor(),
            _ => throw new ArgumentException($"Vista de análisis no soportada: '{view}'.", nameof(view))
        };

    // Visitor interface: defines a visit operation per concrete document element
    private interface IAccountingDocumentVisitor
    {
        (string Kind, decimal Amount) VisitInvoice(InvoiceAnalysisDocument invoice);
        (string Kind, decimal Amount) VisitPayment(PaymentAnalysisDocument payment);
        (string Kind, decimal Amount) VisitJournal(JournalAnalysisDocument journal);
    }

    // ObjectStructure / dispatcher: routes each element to its corresponding Visit* method
    private sealed class DocumentAnalysisAggregator(IAccountingDocumentVisitor visitor)
    {
        private readonly Dictionary<string, decimal> _byKind = new(StringComparer.OrdinalIgnoreCase);
        private decimal _totalAmount;
        private int _documentCount;

        public void Visit(IAccountingAnalysisDocument document)
        {
            (string kind, decimal amount) = document switch
            {
                InvoiceAnalysisDocument invoice => visitor.VisitInvoice(invoice),
                PaymentAnalysisDocument payment => visitor.VisitPayment(payment),
                JournalAnalysisDocument journal => visitor.VisitJournal(journal),
                _ => throw new ArgumentException($"Tipo de documento contable no soportado: '{document.GetType().Name}'.", nameof(document))
            };

            decimal roundedAmount = RoundCurrency(amount);
            _totalAmount = RoundCurrency(_totalAmount + roundedAmount);
            _documentCount++;

            _byKind.TryGetValue(kind, out decimal currentKindTotal);
            _byKind[kind] = RoundCurrency(currentKindTotal + roundedAmount);
        }

        public DocumentAnalysisResult BuildResult() =>
            new(
                RoundCurrency(_totalAmount),
                _documentCount,
                new Dictionary<string, decimal>(_byKind, StringComparer.OrdinalIgnoreCase));
    }

    // Concrete Visitor 1: Gross / total economic value per document
    private sealed class GrossAmountVisitor : IAccountingDocumentVisitor
    {
        public (string Kind, decimal Amount) VisitInvoice(InvoiceAnalysisDocument invoice) =>
            ("invoice", invoice.NetAmount + invoice.TaxAmount);

        public (string Kind, decimal Amount) VisitPayment(PaymentAnalysisDocument payment) =>
            ("payment", payment.Amount);

        public (string Kind, decimal Amount) VisitJournal(JournalAnalysisDocument journal)
        {
            decimal totalDebits = SumDebits(journal.Lines);
            return ("journal", totalDebits);
        }
    }

    // Concrete Visitor 2: Document count per kind
    private sealed class CountDocumentVisitor : IAccountingDocumentVisitor
    {
        public (string Kind, decimal Amount) VisitInvoice(InvoiceAnalysisDocument invoice) =>
            ("invoice", 1m);

        public (string Kind, decimal Amount) VisitPayment(PaymentAnalysisDocument payment) =>
            ("payment", 1m);

        public (string Kind, decimal Amount) VisitJournal(JournalAnalysisDocument journal) =>
            ("journal", 1m);
    }

    // Concrete Visitor 3: Tax / fiscal impact per document
    private sealed class TaxExposureVisitor : IAccountingDocumentVisitor
    {
        public (string Kind, decimal Amount) VisitInvoice(InvoiceAnalysisDocument invoice) =>
            ("invoice", invoice.TaxAmount);

        public (string Kind, decimal Amount) VisitPayment(PaymentAnalysisDocument payment) =>
            ("payment", 0m);

        public (string Kind, decimal Amount) VisitJournal(JournalAnalysisDocument journal) =>
            ("journal", 0m);
    }

    // Concrete Visitor 4: Direct cash / liquidity impact per document
    private sealed class CashImpactVisitor : IAccountingDocumentVisitor
    {
        public (string Kind, decimal Amount) VisitInvoice(InvoiceAnalysisDocument invoice) =>
            ("invoice", 0m);

        public (string Kind, decimal Amount) VisitPayment(PaymentAnalysisDocument payment) =>
            ("payment", payment.Amount);

        public (string Kind, decimal Amount) VisitJournal(JournalAnalysisDocument journal) =>
            ("journal", 0m);
    }

    // Concrete Visitor 5: Accounting posting volume (total debits) per document
    private sealed class LedgerVolumeVisitor : IAccountingDocumentVisitor
    {
        public (string Kind, decimal Amount) VisitInvoice(InvoiceAnalysisDocument invoice) =>
            ("invoice", invoice.NetAmount);

        public (string Kind, decimal Amount) VisitPayment(PaymentAnalysisDocument payment) =>
            ("payment", payment.Amount);

        public (string Kind, decimal Amount) VisitJournal(JournalAnalysisDocument journal) =>
            ("journal", SumDebits(journal.Lines));
    }

    private static decimal SumDebits(IReadOnlyList<AnalysisPostingLine>? lines)
    {
        if (lines is null)
            return 0m;

        decimal total = 0m;
        foreach (AnalysisPostingLine line in lines)
        {
            if (line is not null)
                total += line.Debit;
        }

        return total;
    }

    private static decimal RoundCurrency(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}