using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Catalog.Domain.LegalDocuments;

/// <summary>Văn bản pháp luật (ví dụ 91/2015/QH13), dùng cho nhãn "Cập nhật đến" và tìm kiếm theo số hiệu.</summary>
internal sealed class LegalDocument : AuditableEntity, ISoftDeletable
{
    public LegalDocument(string docNumber, LegalDocumentType docType, string title)
    {
        Update(docNumber, docType, title, issuedDate: null, effectiveDate: null);
    }

    // EF Core
    private LegalDocument()
    {
    }

    public string DocNumber { get; private set; } = string.Empty;

    public LegalDocumentType DocType { get; private set; }

    /// <summary>Trích yếu.</summary>
    public string Title { get; private set; } = string.Empty;

    public DateOnly? IssuedDate { get; private set; }

    public DateOnly? EffectiveDate { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public void Update(
        string docNumber,
        LegalDocumentType docType,
        string title,
        DateOnly? issuedDate,
        DateOnly? effectiveDate)
    {
        DocNumber = Guard.Required(docNumber, "số hiệu văn bản");
        DocType = docType;
        Title = Guard.Required(title, "trích yếu văn bản");
        IssuedDate = issuedDate;
        EffectiveDate = effectiveDate;
    }
}
