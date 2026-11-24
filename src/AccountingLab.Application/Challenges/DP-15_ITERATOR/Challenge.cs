using AccountingLab.Domain.Education;

namespace AccountingLab.Application.Challenges.DP15Iterator;

public sealed record LedgerRecord(string EntryCode, DateOnly Date, decimal Debit, decimal Credit);
public sealed record LedgerPage(IReadOnlyList<LedgerRecord> Records);
public interface ILedgerCursor { bool MoveNext(); LedgerRecord Current { get; } }
public interface ILedgerTraversalChallenge { ILedgerCursor Open(IReadOnlyList<LedgerPage> pages); }
public sealed class LedgerTraversalChallenge : ILedgerTraversalChallenge
{
    public ILedgerCursor Open(IReadOnlyList<LedgerPage> pages)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(pages);

        foreach (LedgerPage page in pages)
        {
            if (page is null)
                throw new ArgumentException("La colección de páginas contiene una página nula.", nameof(pages));

            if (page.Records is null)
                throw new ArgumentException("Una página del libro mayor tiene 'Records' nulo.", nameof(pages));

            foreach (LedgerRecord record in page.Records)
            {
                if (record is null)
                    throw new ArgumentException("Una página contiene un registro nulo.", nameof(pages));
            }
        }

        ILedgerPageCollection collection = new LedgerPageCollection(pages);
        return collection.CreateCursor();
    }

    // Aggregate contract
    private interface ILedgerPageCollection
    {
        ILedgerCursor CreateCursor();
    }

    // ConcreteAggregate
    private sealed class LedgerPageCollection(IReadOnlyList<LedgerPage> pages) : ILedgerPageCollection
    {
        public ILedgerCursor CreateCursor() => new LedgerPagedCursor(pages);
    }

    // ConcreteIterator
    private sealed class LedgerPagedCursor(IReadOnlyList<LedgerPage> pages) : ILedgerCursor
    {
        private int _pageIndex;
        private int _recordIndex = -1;
        private LedgerRecord? _current;

        public LedgerRecord Current =>
            _current ?? throw new InvalidOperationException("No hay un registro activo. Llame a MoveNext() primero o verifique su retorno.");

        public bool MoveNext()
        {
            while (_pageIndex < pages.Count)
            {
                IReadOnlyList<LedgerRecord> records = pages[_pageIndex].Records;
                _recordIndex++;

                if (_recordIndex < records.Count)
                {
                    _current = records[_recordIndex];
                    return true;
                }

                _pageIndex++;
                _recordIndex = -1;
            }

            _current = null;
            return false;
        }
    }
}

