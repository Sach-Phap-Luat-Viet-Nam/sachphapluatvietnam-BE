using Xunit;

namespace SachLuat.IntegrationTests.EndToEnd;

/// <summary>Các test end-to-end dùng chung một host và database, chạy tuần tự.</summary>
[CollectionDefinition(Name)]
public sealed class EndToEndCollection : ICollectionFixture<EndToEndFactory>
{
    public const string Name = "end-to-end";
}
