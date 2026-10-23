namespace SachLuat.Modules.Catalog.Domain;

// Lưu trong DB dạng snake_case (SnakeCaseEnumConverter): đổi tên member là đổi dữ liệu.

internal enum BookType
{
    Code,
    Consolidated,
    Commentary,
    Qna,
    Textbook,
    Monograph,
}

internal enum BookStatus
{
    Draft,
    Active,
    Hidden,
}

internal enum TargetAudience
{
    Student,
    Lawyer,
    Business,
    Citizen,
}

internal enum EditionStatus
{
    Draft,
    Upcoming,
    OnSale,
    Discontinued,
    Clearance,
}

internal enum CoverType
{
    Paperback,
    Hardcover,
}

internal enum LegalDocumentType
{
    Constitution,
    Code,
    Law,
    Ordinance,
    Resolution,
    Decree,
    Decision,
    Circular,
    JointCircular,
    Other,
}
