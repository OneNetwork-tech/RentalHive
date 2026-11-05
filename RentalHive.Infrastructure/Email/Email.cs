using System.Collections.Concurrent;
using System.Threading.Channels;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using RentalHive.Application.Contracts;

namespace RentalHive.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string Section = "Email";

    /// <summary>SMTP host. When empty, nothing is sent: messages are logged and kept in <see cref="EmailOutbox"/>.</summary>
    public string? Host { get; set; }
    public int Port { get; set; } = 587;

    /// <summary>StartTls, SslOnConnect or Auto.</summary>
    public string Security { get; set; } = "StartTls";
    public string? Username { get; set; }

    /// <summary>Set through the environment (Email__Password), never a committed file.</summary>
    public string? Password { get; set; }
    public string From { get; set; } = "no-reply@localhost";
    public string FromName { get; set; } = "RentalHive";
}

/// <summary>The last messages handled, for development and tests.</summary>
public sealed class EmailOutbox
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public void Add(EmailMessage message)
    {
        _messages.Enqueue(message);
        while (_messages.Count > 200 && _messages.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<EmailMessage> All => _messages.ToArray();
}

public sealed class BackgroundEmailSender : IEmailSender
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private readonly ILogger<BackgroundEmailSender> _logger;

    public BackgroundEmailSender(ILogger<BackgroundEmailSender> logger) => _logger = logger;

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public void Enqueue(EmailMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.To)) return;
        if (!_channel.Writer.TryWrite(message)) _logger.LogWarning("Email queue is full; dropped email '{Subject}'", message.Subject);
    }
}

/// <summary>Delivers queued email in the background, one message at a time.</summary>
public sealed partial class EmailDispatcher(BackgroundEmailSender queue, EmailOutbox outbox, IOptions<EmailOptions> options, IHostEnvironment env, ILogger<EmailDispatcher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.Host) && !env.IsDevelopment())
            LogNotConfigured(logger);

        try
        {
            await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                outbox.Add(message);
                try
                {
                    await SendAsync(o, message, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogSendFailed(logger, ex, message.Subject);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }

    private async Task SendAsync(EmailOptions o, EmailMessage message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(o.Host))
        {
            // Bodies can contain contact details; print them only in Development.
            if (env.IsDevelopment()) LogDevEmail(logger, message.To, message.Subject, message.Body);
            return;
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(o.FromName, o.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };

        using var client = new SmtpClient();
        var security = Enum.TryParse<SecureSocketOptions>(o.Security, true, out var s) ? s : SecureSocketOptions.StartTls;
        await client.ConnectAsync(o.Host, o.Port, security, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(o.Username)) await client.AuthenticateAsync(o.Username, o.Password ?? string.Empty, ct).ConfigureAwait(false);
        await client.SendAsync(mime, ct).ConfigureAwait(false);
        await client.DisconnectAsync(true, ct).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email:Host is not set: booking notifications are not sent.")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not send email '{Subject}'")]
    private static partial void LogSendFailed(ILogger logger, Exception ex, string subject);

    [LoggerMessage(Level = LogLevel.Information, Message = "Email (SMTP not configured) to {To}: {Subject}\n{Body}")]
    private static partial void LogDevEmail(ILogger logger, string to, string subject, string body);
}
