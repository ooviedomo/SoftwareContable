using AccountingLab.Domain.Education;

namespace AccountingLab.Domain.Challenges.DP11Flyweight;

public sealed record MovementClassificationInput(Guid Id, decimal Amount, string Code, string Label, string FictionalTaxScope);
public sealed record ClassificationMetadata(string Code, string Label, string FictionalTaxScope);
public sealed record ClassifiedMovement(Guid Id, decimal Amount, ClassificationMetadata Metadata);
public interface ISharedClassificationChallenge { IReadOnlyList<ClassifiedMovement> Prepare(IReadOnlyList<MovementClassificationInput> movements); }
public sealed class SharedClassificationChallenge : ISharedClassificationChallenge
{
    public IReadOnlyList<ClassifiedMovement> Prepare(IReadOnlyList<MovementClassificationInput> movements)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(movements);

        ClassificationMetadataFactory metadataFactory = new();
        List<ClassifiedMovement> result = new(movements.Count);

        foreach (MovementClassificationInput movement in movements)
        {
            ArgumentNullException.ThrowIfNull(movement);

            string code = movement.Code ?? string.Empty;
            string label = movement.Label ?? string.Empty;
            string taxScope = movement.FictionalTaxScope ?? string.Empty;

            // Estado intrínseco compartido (Flyweight) vs. estado extrínseco por movimiento (Id, Amount).
            ClassificationMetadata sharedMetadata = metadataFactory.GetOrCreate(code, label, taxScope);
            result.Add(new ClassifiedMovement(movement.Id, movement.Amount, sharedMetadata));
        }

        return result.AsReadOnly();
    }

    // Flyweight Factory
    private sealed class ClassificationMetadataFactory
    {
        private readonly Dictionary<(string Code, string Label, string FictionalTaxScope), ClassificationMetadata> _pool = new();

        public ClassificationMetadata GetOrCreate(string code, string label, string fictionalTaxScope)
        {
            var key = (code, label, fictionalTaxScope);

            if (!_pool.TryGetValue(key, out ClassificationMetadata? metadata))
            {
                metadata = new ClassificationMetadata(code, label, fictionalTaxScope);
                _pool[key] = metadata;
            }

            return metadata;
        }
    }
}
