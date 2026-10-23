using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace SachLuat.Modules.Ordering.Infrastructure.Persistence;

/// <summary>Sinh mã đơn <c>SLVN2610230001</c> (mục 10.2): tiền tố + ngày giờ Việt Nam + số thứ tự trong ngày.</summary>
internal sealed class OrderCodeGenerator(OrderingDbContext dbContext, TimeProvider timeProvider)
{
    public const string Prefix = "SLVN";

    // Việt Nam không có giờ mùa hè nên dùng offset cố định, không phụ thuộc tz database của hệ điều hành.
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<string> NextAsync(CancellationToken cancellationToken)
    {
        var day = DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(VietnamOffset).DateTime);

        // Một câu lệnh nguyên tử: nhiều đơn cùng lúc không bị trùng số.
        var values = await dbContext.Database
            .SqlQuery<int>($"""
                INSERT INTO ordering.order_code_counters (day, last_value) VALUES ({day}, 1)
                ON CONFLICT (day) DO UPDATE SET last_value = order_code_counters.last_value + 1
                RETURNING last_value AS "Value"
                """)
            .ToListAsync(cancellationToken);

        return Prefix
            + day.ToString("yyMMdd", CultureInfo.InvariantCulture)
            + values[0].ToString("D4", CultureInfo.InvariantCulture);
    }
}
