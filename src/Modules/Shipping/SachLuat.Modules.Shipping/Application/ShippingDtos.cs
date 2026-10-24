namespace SachLuat.Modules.Shipping.Application;

/// <param name="Provinces">Tên tỉnh/thành; so khớp không phân biệt hoa thường, dấu và tiền tố "Thành phố"/"Tỉnh".</param>
internal sealed record UpsertZoneRequest(
    string Name,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<string> Provinces,
    int BaseWeightGram,
    decimal BaseFee,
    int StepWeightGram,
    decimal StepFee,
    int EtaMinDays,
    int EtaMaxDays);

internal sealed record ZoneResponse(
    long Id,
    string Name,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<string> Provinces,
    int BaseWeightGram,
    decimal BaseFee,
    int StepWeightGram,
    decimal StepFee,
    int EtaMinDays,
    int EtaMaxDays,
    DateTime UpdatedAt);

/// <param name="FreeShippingThreshold">null: tắt miễn phí ship theo ngưỡng.</param>
internal sealed record ShippingSettingsRequest(
    decimal? FreeShippingThreshold,
    int PackagingWeightGram,
    int DefaultBookWeightGram,
    decimal FlatFee);

/// <param name="IsDefault">true khi Admin chưa lưu lần nào, đang dùng giá trị mặc định từ cấu hình.</param>
internal sealed record ShippingSettingsResponse(
    decimal? FreeShippingThreshold,
    int PackagingWeightGram,
    int DefaultBookWeightGram,
    decimal FlatFee,
    bool IsDefault);
