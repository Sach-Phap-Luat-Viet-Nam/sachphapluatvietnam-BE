using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SachLuat.SharedKernel.Persistence;

public static class DbUpdateExceptionExtensions
{
    /// <summary>Vi phạm unique index/constraint (SQLSTATE 23505), lọc theo tên nếu có.</summary>
    public static bool IsUniqueViolation(this DbUpdateException exception, string? constraintName = null) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && (constraintName is null || postgres.ConstraintName == constraintName);
}
