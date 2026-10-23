namespace SachLuat.SharedKernel.Domain;

public interface IAuditable
{
    DateTime CreatedAt { get; }

    DateTime UpdatedAt { get; }
}
