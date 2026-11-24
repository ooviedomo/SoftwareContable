using System.Globalization;
using System.Text.Json;
using AccountingLab.Domain.Education;

namespace AccountingLab.Infrastructure.Challenges.DP06Adapter;

public sealed record BankImportRequest(string Provider, string Payload);
public sealed record ImportedBankMovement(string ExternalId, DateOnly Date, string Description, decimal Debit, decimal Credit, string Currency);
public interface IBankImportChallenge { IReadOnlyList<ImportedBankMovement> Import(BankImportRequest request); }
public sealed class BankImportChallenge : IBankImportChallenge
{
    public IReadOnlyList<ImportedBankMovement> Import(BankImportRequest request)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Provider))
            throw new ArgumentException("El proveedor bancario es obligatorio.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Payload))
            throw new ArgumentException("El contenido a importar es obligatorio.", nameof(request));

        IBankStatementAdapter adapter = ResolveAdapter(request.Provider);
        return adapter.Adapt(request.Payload);
    }

    private static IBankStatementAdapter ResolveAdapter(string provider) =>
        provider.Trim().ToLowerInvariant() switch
        {
            "bancoa" => new BancoABankAdapter(new BancoAFeed()),
            "bancob" => new BancoBBankAdapter(new BancoBFeed()),
            _ => throw new ArgumentException($"Proveedor bancario no soportado: '{provider}'.", nameof(provider))
        };

    // Target interface
    private interface IBankStatementAdapter
    {
        IReadOnlyList<ImportedBankMovement> Adapt(string payload);
    }

    // Adapter BancoA -> ImportedBankMovement
    private sealed class BancoABankAdapter(BancoAFeed feed) : IBankStatementAdapter
    {
        public IReadOnlyList<ImportedBankMovement> Adapt(string payload) =>
            feed.ReadMovements(payload)
                .Select(static item => new ImportedBankMovement(
                    ExternalId: item.Id,
                    Date: item.Date,
                    Description: item.Description,
                    Debit: item.Debit,
                    Credit: item.Credit,
                    Currency: item.Currency
                ))
                .ToList()
                .AsReadOnly();
    }

    // Adapter BancoB -> ImportedBankMovement
    private sealed class BancoBBankAdapter(BancoBFeed feed) : IBankStatementAdapter
    {
        public IReadOnlyList<ImportedBankMovement> Adapt(string payload) =>
            feed.ReadMovements(payload)
                .Select(static item => new ImportedBankMovement(
                    ExternalId: item.Reference,
                    Date: item.BookedOn,
                    Description: item.Concept,
                    Debit: item.SignedAmount < 0m ? Math.Abs(item.SignedAmount) : 0m,
                    Credit: item.SignedAmount > 0m ? item.SignedAmount : 0m,
                    Currency: item.IsoCurrency
                ))
                .ToList()
                .AsReadOnly();
    }

    // Adaptee 1: BancoA (id, date, description, debit, credit, currency)
    private sealed record BancoARawItem(string Id, DateOnly Date, string Description, decimal Debit, decimal Credit, string Currency);

    private sealed class BancoAFeed
    {
        public IReadOnlyList<BancoARawItem> ReadMovements(string jsonPayload)
        {
            JsonElement root = ParseRootElement(jsonPayload);
            JsonElement array = ExtractArray(root);
            List<BancoARawItem> items = [];

            foreach (JsonElement element in array.EnumerateArray())
            {
                string id = ReadRequiredString(element, "id");
                DateOnly date = ReadRequiredDate(element, "date");
                string description = ReadRequiredString(element, "description");
                decimal debit = ReadRequiredDecimal(element, "debit");
                decimal credit = ReadRequiredDecimal(element, "credit");
                string currency = ReadRequiredString(element, "currency");

                items.Add(new BancoARawItem(id, date, description, debit, credit, currency));
            }

            return items;
        }
    }

    // Adaptee 2: BancoB (reference, bookedOn, concept, signedAmount, isoCurrency)
    private sealed record BancoBRawItem(string Reference, DateOnly BookedOn, string Concept, decimal SignedAmount, string IsoCurrency);

    private sealed class BancoBFeed
    {
        public IReadOnlyList<BancoBRawItem> ReadMovements(string jsonPayload)
        {
            JsonElement root = ParseRootElement(jsonPayload);
            JsonElement array = ExtractArray(root);
            List<BancoBRawItem> items = [];

            foreach (JsonElement element in array.EnumerateArray())
            {
                string reference = ReadRequiredString(element, "reference");
                DateOnly bookedOn = ReadRequiredDate(element, "bookedOn");
                string concept = ReadRequiredString(element, "concept");
                decimal signedAmount = ReadRequiredDecimal(element, "signedAmount");
                string isoCurrency = ReadRequiredString(element, "isoCurrency");

                items.Add(new BancoBRawItem(reference, bookedOn, concept, signedAmount, isoCurrency));
            }

            return items;
        }
    }

    private static JsonElement ParseRootElement(string jsonPayload)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(jsonPayload);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new FormatException("El contenido JSON no es válido.", ex);
        }
    }

    private static JsonElement ExtractArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root;

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (string candidate in new[] { "movements", "items", "transactions", "data" })
            {
                if (root.TryGetProperty(candidate, out JsonElement prop) && prop.ValueKind == JsonValueKind.Array)
                    return prop;
            }
        }

        throw new FormatException("El JSON bancario debe contener un arreglo de movimientos.");
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement prop))
            throw new FormatException($"Falta la propiedad '{propertyName}'.");

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? throw new FormatException($"La propiedad '{propertyName}' es nula."),
            JsonValueKind.Number => prop.GetRawText(),
            _ => throw new FormatException($"La propiedad '{propertyName}' tiene un tipo no válido.")
        };
    }

    private static DateOnly ReadRequiredDate(JsonElement element, string propertyName)
    {
        string raw = ReadRequiredString(element, propertyName).Trim();
        if (!DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
            throw new FormatException($"La fecha '{raw}' en '{propertyName}' no es válida.");

        return date;
    }

    private static decimal ReadRequiredDecimal(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement prop))
            throw new FormatException($"Falta la propiedad '{propertyName}'.");

        try
        {
            return prop.ValueKind switch
            {
                JsonValueKind.String => decimal.Parse(prop.GetString()!.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture),
                JsonValueKind.Number => prop.GetDecimal(),
                _ => throw new FormatException($"El importe en '{propertyName}' no es válido.")
            };
        }
        catch (Exception ex) when (ex is not FormatException)
        {
            throw new FormatException($"El importe en '{propertyName}' no es un decimal válido.", ex);
        }
    }
}