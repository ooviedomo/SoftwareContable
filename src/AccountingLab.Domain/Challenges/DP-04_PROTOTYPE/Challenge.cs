using AccountingLab.Domain.Education;

namespace AccountingLab.Domain.Challenges.DP04Prototype;

public sealed class TemplateLine
{
    public string AccountCode { get; set; } = "";
    public decimal Amount { get; set; }
    public List<string> Tags { get; set; } = new();
}
public sealed class RecurringTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<TemplateLine> Lines { get; set; } = new();
}
public interface ITemplateCopyChallenge { RecurringTemplate Copy(RecurringTemplate source); }
public sealed class TemplateCopyChallenge : ITemplateCopyChallenge
{
    public RecurringTemplate Copy(RecurringTemplate source)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(source);

        IRecurringTemplatePrototype prototype = new RecurringTemplatePrototype(source);
        return prototype.Clone();
    }

    private interface IRecurringTemplatePrototype
    {
        RecurringTemplate Clone();
    }

    private sealed class RecurringTemplatePrototype(RecurringTemplate source) : IRecurringTemplatePrototype
    {
        public RecurringTemplate Clone() =>
            new()
            {
                Id = Guid.NewGuid(),
                Name = source.Name,
                Lines = source.Lines
                    .Where(static line => line is not null)
                    .Select(static line => new TemplateLinePrototype(line).Clone())
                    .ToList()
            };
    }

    private sealed class TemplateLinePrototype(TemplateLine source) : IRecurringTemplatePrototype<TemplateLine>
    {
        public TemplateLine Clone() =>
            new()
            {
                AccountCode = source.AccountCode,
                Amount = source.Amount,
                Tags = source.Tags is not null ? new List<string>(source.Tags) : new List<string>()
            };
    }

    private interface IRecurringTemplatePrototype<out T>
    {
        T Clone();
    }
}