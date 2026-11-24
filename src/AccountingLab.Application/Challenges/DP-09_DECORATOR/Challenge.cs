using System.Security.Cryptography;
using System.Text;
using AccountingLab.Domain.Education;

namespace AccountingLab.Application.Challenges.DP09Decorator;

public sealed record DocumentExportRequest(string Body, IReadOnlyList<string> Features, IReadOnlyDictionary<string, string> Metadata);
public sealed record ExportedAccountingDocument(string Content, IReadOnlyList<string> AppliedFeatures, IReadOnlyDictionary<string, string> Metadata);
public interface IDocumentEnrichmentChallenge { ExportedAccountingDocument Export(DocumentExportRequest request); }
public sealed class DocumentEnrichmentChallenge : IDocumentEnrichmentChallenge
{
    public ExportedAccountingDocument Export(DocumentExportRequest request)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ArgumentException("El cuerpo del documento es obligatorio.", nameof(request));

        IAccountingDocumentExporter exporter = new BaseAccountingDocumentExporter(request.Body, request.Metadata);

        if (request.Features is not null)
        {
            foreach (string rawFeature in request.Features)
            {
                if (string.IsNullOrWhiteSpace(rawFeature))
                    throw new ArgumentException("El nombre de la característica no puede estar vacío.", nameof(request));

                string trimmedFeature = rawFeature.Trim();
                exporter = WrapWithFeature(exporter, trimmedFeature);
            }
        }

        return exporter.Export();
    }

    private static IAccountingDocumentExporter WrapWithFeature(IAccountingDocumentExporter inner, string feature) =>
        feature.ToLowerInvariant() switch
        {
            "auditoria" or "auditoría" or "audit" or "audit-stamp" => new AuditStampDocumentDecorator(inner, feature),
            "metadatos" or "metadata" => new MetadataDocumentDecorator(inner, feature),
            "header" or "encabezado" => new HeaderDocumentDecorator(inner, feature),
            "footer" or "pie" => new FooterDocumentDecorator(inner, feature),
            "watermark" or "marca-agua" or "marca_agua" => new WatermarkDocumentDecorator(inner, feature),
            "signature" or "firma" => new SignatureDocumentDecorator(inner, feature),
            "hash" or "checksum" => new ChecksumDocumentDecorator(inner, feature),
            _ => throw new ArgumentException($"Característica de exportación no soportada: '{feature}'.", nameof(feature))
        };

    // Component
    private interface IAccountingDocumentExporter
    {
        ExportedAccountingDocument Export();
    }

    // Concrete Component
    private sealed class BaseAccountingDocumentExporter(
        string body,
        IReadOnlyDictionary<string, string>? initialMetadata) : IAccountingDocumentExporter
    {
        public ExportedAccountingDocument Export()
        {
            Dictionary<string, string> metadataCopy = initialMetadata is not null
                ? new Dictionary<string, string>(initialMetadata, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            return new ExportedAccountingDocument(body, Array.Empty<string>(), metadataCopy);
        }
    }

    // Base Decorator
    private abstract class AccountingDocumentDecorator(
        IAccountingDocumentExporter inner,
        string featureName) : IAccountingDocumentExporter
    {
        protected IAccountingDocumentExporter Inner { get; } = inner;
        protected string FeatureName { get; } = featureName;

        public ExportedAccountingDocument Export()
        {
            ExportedAccountingDocument current = Inner.Export();

            List<string> features = [.. current.AppliedFeatures, FeatureName];
            Dictionary<string, string> metadata = new(current.Metadata, StringComparer.OrdinalIgnoreCase);

            string enrichedContent = ApplyEnrichment(current.Content, metadata);

            return new ExportedAccountingDocument(enrichedContent, features.AsReadOnly(), metadata);
        }

        protected abstract string ApplyEnrichment(string content, IDictionary<string, string> metadata);
    }

    // Concrete Decorators
    private sealed class AuditStampDocumentDecorator(IAccountingDocumentExporter inner, string featureName)
        : AccountingDocumentDecorator(inner, featureName)
    {
        protected override string ApplyEnrichment(string content, IDictionary<string, string> metadata)
        {
            string auditId = metadata.TryGetValue("auditId", out string? id) && !string.IsNullOrWhiteSpace(id)
                ? id
                : "AUDIT-VERIFIED";

            metadata["audit"] = auditId;
            return $"{content}\n[AUDIT] {auditId}";
        }
    }

    private sealed class MetadataDocumentDecorator(IAccountingDocumentExporter inner, string featureName)
        : AccountingDocumentDecorator(inner, featureName)
    {
        protected override string ApplyEnrichment(string content, IDictionary<string, string> metadata)
        {
            if (metadata.Count == 0)
            {
                metadata["metadata"] = "none";
                return $"{content}\n[METADATA] none";
            }

            string formattedEntries = string.Join(", ", metadata.Select(kv => $"{kv.Key}={kv.Value}"));
            return $"{content}\n[METADATA] {formattedEntries}";
        }
    }

    private sealed class HeaderDocumentDecorator(IAccountingDocumentExporter inner, string featureName)
        : AccountingDocumentDecorator(inner, featureName)
    {
        protected override string ApplyEnrichment(string content, IDictionary<string, string> metadata)
        {
            string title = metadata.TryGetValue("title", out string? t) && !string.IsNullOrWhiteSpace(t)
                ? t
                : "DOCUMENTO CONTABLE";

            metadata["header"] = title;
            return $"=== {title} ===\n{content}";
        }
    }

    private sealed class FooterDocumentDecorator(IAccountingDocumentExporter inner, string featureName)
        : AccountingDocumentDecorator(inner, featureName)
    {
        protected override string ApplyEnrichment(string content, IDictionary<string, string> metadata)
        {
            string footer = metadata.TryGetValue("footer", out string? f) && !string.IsNullOrWhiteSpace(f)
                ? f
                : "AccountingLab - Documento Oficial";

            metadata["footer"] = footer;
            return $"{content}\n--- {footer} ---";
        }
    }

    private sealed class WatermarkDocumentDecorator(IAccountingDocumentExporter inner, string featureName)
        : AccountingDocumentDecorator(inner, featureName)
    {
        protected override string ApplyEnrichment(string content, IDictionary<string, string> metadata)
        {
            string watermark = metadata.TryGetValue("watermark", out string? w) && !string.IsNullOrWhiteSpace(w)
                ? w
                : "CONFIDENCIAL";

            metadata["watermark"] = watermark;
            return $"[WATERMARK: {watermark}]\n{content}";
        }
    }

    private sealed class SignatureDocumentDecorator(IAccountingDocumentExporter inner, string featureName)
        : AccountingDocumentDecorator(inner, featureName)
    {
        protected override string ApplyEnrichment(string content, IDictionary<string, string> metadata)
        {
            string signer = metadata.TryGetValue("signer", out string? s) && !string.IsNullOrWhiteSpace(s)
                ? s
                : "Contador Autorizado";

            metadata["signedBy"] = signer;
            return $"{content}\n[Firma: {signer}]";
        }
    }

    private sealed class ChecksumDocumentDecorator(IAccountingDocumentExporter inner, string featureName)
        : AccountingDocumentDecorator(inner, featureName)
    {
        protected override string ApplyEnrichment(string content, IDictionary<string, string> metadata)
        {
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            string hexHash = Convert.ToHexString(hashBytes);

            metadata["checksum"] = hexHash;
            return $"{content}\n[SHA256: {hexHash}]";
        }
    }
}