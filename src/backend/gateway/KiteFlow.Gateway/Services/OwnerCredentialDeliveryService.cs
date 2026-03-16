using System.Net;
using System.Net.Mail;
using System.Text;
using KiteFlow.Gateway.Configuration;
using Microsoft.Extensions.Options;

namespace KiteFlow.Gateway.Services;

public interface IOwnerCredentialDeliveryService
{
    Task<OwnerCredentialDeliveryResult> SendTemporaryPasswordAsync(
        OwnerCredentialDeliveryMessage message,
        CancellationToken cancellationToken);
}

public sealed class OwnerCredentialDeliveryService : IOwnerCredentialDeliveryService
{
    private readonly OwnerCredentialDeliveryOptions _options;

    public OwnerCredentialDeliveryService(IOptions<OwnerCredentialDeliveryOptions> options)
    {
        _options = options.Value;
    }

    public async Task<OwnerCredentialDeliveryResult> SendTemporaryPasswordAsync(
        OwnerCredentialDeliveryMessage message,
        CancellationToken cancellationToken)
    {
        var mode = (_options.Mode ?? "File").Trim();

        if (mode.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return new OwnerCredentialDeliveryResult("Disabled", false, null);
        }

        if (mode.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            await SendBySmtpAsync(message, cancellationToken);
            return new OwnerCredentialDeliveryResult("Smtp", true, null);
        }

        var outboxPath = await SaveToOutboxAsync(message, cancellationToken);
        return new OwnerCredentialDeliveryResult("File", true, outboxPath);
    }

    private async Task<string> SaveToOutboxAsync(
        OwnerCredentialDeliveryMessage message,
        CancellationToken cancellationToken)
    {
        var outboxDirectory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(_options.OutboxDirectory) ? "temp/email-outbox" : _options.OutboxDirectory!,
            Directory.GetCurrentDirectory());

        Directory.CreateDirectory(outboxDirectory);

        var fileName =
            $"{DateTime.UtcNow:yyyyMMddHHmmss}_{SanitizeFileName(message.OwnerEmail)}_{message.SchoolSlug}.txt";
        var filePath = Path.Combine(outboxDirectory, fileName);

        var content = BuildPlainTextBody(message);
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken);
        return filePath;
    }

    private async Task SendBySmtpAsync(
        OwnerCredentialDeliveryMessage message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            throw new InvalidOperationException("O host SMTP não foi configurado.");
        }

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = "Acesso inicial à plataforma Quiver",
            Body = BuildPlainTextBody(message),
            IsBodyHtml = false
        };

        mailMessage.To.Add(new MailAddress(message.OwnerEmail, message.OwnerFullName));

        using var smtpClient = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.SmtpUseSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.SmtpUsername))
        {
            smtpClient.Credentials = new NetworkCredential(
                _options.SmtpUsername,
                _options.SmtpPassword);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await smtpClient.SendMailAsync(mailMessage, cancellationToken);
    }

    private static string BuildPlainTextBody(OwnerCredentialDeliveryMessage message)
    {
        var lines = new[]
        {
            $"Olá, {message.OwnerFullName}.",
            string.Empty,
            $"Sua escola \"{message.SchoolDisplayName}\" foi criada na plataforma Quiver.",
            "Use os dados abaixo para acessar pela primeira vez:",
            string.Empty,
            $"URL: {message.LoginUrl}",
            $"E-mail: {message.OwnerEmail}",
            $"Senha temporária: {message.TemporaryPassword}",
            string.Empty,
            "No primeiro acesso, a troca da senha será obrigatória.",
            string.Empty,
            "Se você não esperava este e-mail, fale com o administrador da plataforma."
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
    }
}

public sealed record OwnerCredentialDeliveryMessage(
    string OwnerFullName,
    string OwnerEmail,
    string SchoolDisplayName,
    string SchoolSlug,
    string TemporaryPassword,
    string LoginUrl);

public sealed record OwnerCredentialDeliveryResult(
    string Mode,
    bool Delivered,
    string? OutboxFilePath);
