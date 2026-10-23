namespace SachLuat.Modules.Catalog.Domain.Editions;

/// <summary>Thông tin xuất bản bắt buộc của ấn bản (BR-04: phải có ISBN và số QĐXB).</summary>
internal sealed record EditionPublishingInfo(
    string Isbn,
    string Publisher,
    string PublishingDecisionNo,
    string? RegistrationNo = null,
    string? EditionName = null,
    short? PublishYear = null);
