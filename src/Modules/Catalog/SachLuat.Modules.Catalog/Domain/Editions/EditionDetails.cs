namespace SachLuat.Modules.Catalog.Domain.Editions;

internal sealed record EditionDetails(
    string? UpdatedToText = null,
    DateOnly? UpdatedToDate = null,
    int? Pages = null,
    string? Size = null,
    int? WeightGram = null,
    string? CoverImageUrl = null,
    string? PreviewFileUrl = null,
    string? TableOfContents = null,
    DateOnly? ReleaseDate = null);
