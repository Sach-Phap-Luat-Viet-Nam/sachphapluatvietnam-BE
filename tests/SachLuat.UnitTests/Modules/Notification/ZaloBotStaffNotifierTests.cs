using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Notification.Contracts;
using SachLuat.Modules.Notification.Infrastructure.Zalo;
using Xunit;

namespace SachLuat.UnitTests.Modules.Notification;

public sealed class ZaloBotStaffNotifierTests
{
    private const string Token = "12345:secret-token";
    private const string ChatId = "group-chat-id";

    [Fact]
    public async Task SendAsync_Ok_PostsChatIdAndTextToSendMessage()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"ok":true,"result":{"message_id":"m1","date":1}}""");

        await CreateNotifier(handler).SendAsync("Đơn mới", TestContext.Current.CancellationToken);

        handler.RequestUri.Should().Be(new Uri($"https://bot-api.test/bot{Token}/sendMessage"));
        handler.Method.Should().Be(HttpMethod.Post);
        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("chat_id").GetString().Should().Be(ChatId);
        body.RootElement.GetProperty("text").GetString().Should().Be("Đơn mới");
    }

    [Fact]
    public async Task SendAsync_ApiReturnsOkFalse_ThrowsWithDescription()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK, """{"ok":false,"error_code":403,"description":"Bot is not a member of the chat"}""");

        var act = () => CreateNotifier(handler).SendAsync("x", TestContext.Current.CancellationToken);

        var exception = (await act.Should().ThrowAsync<StaffNotificationException>()).Which;
        exception.Message.Should().Contain("403").And.Contain("Bot is not a member of the chat");
        exception.Message.Should().NotContain(Token);
    }

    [Fact]
    public async Task SendAsync_ServerErrorWithHtmlBody_Throws()
    {
        var handler = new StubHandler(HttpStatusCode.BadGateway, "<html>Bad gateway</html>", "text/html");

        var act = () => CreateNotifier(handler).SendAsync("x", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<StaffNotificationException>()).Which.Message.Should().Contain("502");
    }

    [Fact]
    public async Task SendAsync_NetworkError_WrapsException()
    {
        var handler = new StubHandler(new HttpRequestException("Connection refused"));

        var act = () => CreateNotifier(handler).SendAsync("x", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<StaffNotificationException>())
            .WithInnerException<HttpRequestException>();
    }

    [Fact]
    public async Task SendAsync_Timeout_WrapsException()
    {
        var handler = new StubHandler(new TaskCanceledException("timeout"));

        var act = () => CreateNotifier(handler).SendAsync("x", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<StaffNotificationException>()).Which.Message.Should().Contain("thời gian");
    }

    [Fact]
    public async Task SendAsync_Disabled_Throws()
    {
        var notifier = CreateNotifier(new StubHandler(HttpStatusCode.OK, "{}"), enabled: false);

        notifier.IsEnabled.Should().BeFalse();
        await notifier.Invoking(n => n.SendAsync("x", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Truncate_LongText_CutsToLimitWithoutSplittingEmoji()
    {
        var text = new string('a', ZaloBotStaffNotifier.MaxTextLength - 2) + "🛒" + "tail";

        var truncated = ZaloBotStaffNotifier.Truncate(text);

        truncated.Length.Should().BeLessThanOrEqualTo(ZaloBotStaffNotifier.MaxTextLength);
        truncated.Should().EndWith("a…");
        ZaloBotStaffNotifier.Truncate("ngắn").Should().Be("ngắn");
    }

    private static ZaloBotStaffNotifier CreateNotifier(StubHandler handler, bool enabled = true) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://bot-api.test/") },
        Options.Create(new ZaloBotOptions { Enabled = enabled, BotToken = Token, ChatId = ChatId }),
        NullLogger<ZaloBotStaffNotifier>.Instance);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody = string.Empty;
        private readonly string _mediaType = "application/json";
        private readonly Exception? _exception;

        public StubHandler(HttpStatusCode statusCode, string responseBody, string mediaType = "application/json")
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
            _mediaType = mediaType;
        }

        public StubHandler(Exception exception) => _exception = exception;

        public Uri? RequestUri { get; private set; }

        public HttpMethod? Method { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (_exception is not null)
            {
                throw _exception;
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, _mediaType),
            };
        }
    }
}
