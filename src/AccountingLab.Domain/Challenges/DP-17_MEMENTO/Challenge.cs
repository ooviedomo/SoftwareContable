using AccountingLab.Domain.Education;

namespace AccountingLab.Domain.Challenges.DP17Memento;

public sealed class BudgetDraftLine
{
    public string AccountCode { get; set; } = "";
    public decimal Amount { get; set; }
}
public sealed class BudgetDraft
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool Approved { get; set; }
    public List<BudgetDraftLine> Lines { get; set; } = new();
}
public interface IBudgetSnapshot { }
public interface IBudgetHistoryChallenge
{
    IBudgetSnapshot Save(BudgetDraft draft);
    BudgetDraft Restore(BudgetDraft current, IBudgetSnapshot snapshot);
}
public sealed class BudgetHistoryChallenge : IBudgetHistoryChallenge
{
    public IBudgetSnapshot Save(BudgetDraft draft)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(draft);

        if (draft.Approved)
            throw new InvalidOperationException("Un presupuesto aprobado no admite guardar un borrador.");

        BudgetDraftOriginator originator = new(draft);
        return originator.CreateSnapshot();
    }

    public BudgetDraft Restore(BudgetDraft current, IBudgetSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (current.Approved)
            throw new InvalidOperationException("Un presupuesto aprobado no admite restaurar un borrador.");

        if (snapshot is not BudgetDraftMemento memento)
            throw new ArgumentException("El snapshot proporcionado no corresponde a un memento válido de borrador de presupuesto.", nameof(snapshot));

        BudgetDraftOriginator originator = new(current);
        originator.Restore(memento);
        return originator.Draft;
    }

    // Originator: captures and restores internal state without exposing internals externally
    private sealed class BudgetDraftOriginator(BudgetDraft draft)
    {
        public BudgetDraft Draft { get; private set; } = draft;

        public IBudgetSnapshot CreateSnapshot()
        {
            List<BudgetDraftLine> clonedLines = CloneLines(Draft.Lines);
            return new BudgetDraftMemento(Draft.Id, Draft.Name, Draft.Approved, clonedLines);
        }

        public void Restore(BudgetDraftMemento memento)
        {
            List<BudgetDraftLine> restoredLines = CloneLines(memento.Lines);

            Draft.Id = memento.Id;
            Draft.Name = memento.Name;
            Draft.Approved = memento.Approved;
            Draft.Lines = restoredLines;
        }

        private static List<BudgetDraftLine> CloneLines(IEnumerable<BudgetDraftLine>? lines)
        {
            if (lines is null)
                return [];

            List<BudgetDraftLine> copy = [];
            foreach (BudgetDraftLine line in lines)
            {
                if (line is null)
                    throw new ArgumentException("Una línea del borrador de presupuesto es nula.");

                copy.Add(new BudgetDraftLine
                {
                    AccountCode = line.AccountCode,
                    Amount = line.Amount
                });
            }

            return copy;
        }
    }

    // Memento: opaque state container implementing IBudgetSnapshot
    private sealed class BudgetDraftMemento(
        Guid id,
        string name,
        bool approved,
        List<BudgetDraftLine> lines) : IBudgetSnapshot
    {
        public Guid Id { get; } = id;
        public string Name { get; } = name;
        public bool Approved { get; } = approved;
        public List<BudgetDraftLine> Lines { get; } = lines;
    }
}