using System.Net;
using System.Net.Mail;
using System.Text;
using KiteFlow.Services.Identity.Api.Configuration;
using Microsoft.Extensions.Options;

namespace KiteFlow.Services.Identity.Api.Services;

public interface IIdentityEmailDeliveryService
{
    Task<IdentityEmailDeliveryResult> SendInvitationAsync(
        InvitationEmailMessage message,
        CancellationToken cancellationToken);

    Task<IdentityEmailDeliveryResult> SendPasswordResetAsync(
        PasswordResetEmailMessage message,
        CancellationToken cancellationToken);

    Task<IdentityEmailDeliveryResult> SendTemporaryPasswordAsync(
        TemporaryPasswordEmailMessage message,
        CancellationToken cancellationToken);
}

public sealed class IdentityEmailDeliveryService : IIdentityEmailDeliveryService
{
    private readonly IdentityEmailDeliveryOptions _options;

    public IdentityEmailDeliveryService(IOptions<IdentityEmailDeliveryOptions> options)
    {
        _options = options.Value;
    }

    public Task<IdentityEmailDeliveryResult> SendInvitationAsync(
        InvitationEmailMessage message,
        CancellationToken cancellationToken)
        => SendAsync(
            recipientEmail: message.Email,
            recipientName: message.FullName,
            subject: "Convite para acessar a plataforma Quiver",
            body: BuildInvitationBody(message),
            filePrefix: "invitation",
            slug: message.SchoolSlug,
            cancellationToken);

    public Task<IdentityEmailDeliveryResult> SendPasswordResetAsync(
        PasswordResetEmailMessage message,
        CancellationToken cancellationToken)
        => SendAsync(
            recipientEmail: message.Email,
            recipientName: message.FullName,
            subject: "Recuperação de acesso à plataforma Quiver",
            body: BuildPasswordResetBody(message),
            filePrefix: "password-reset",
            slug: message.ScopeLabel,
            cancellationToken);

    public Task<IdentityEmailDeliveryResult> SendTemporaryPasswordAsync(
        TemporaryPasswordEmailMessage message,
        CancellationToken cancellationToken)
        => SendAsync(
            recipientEmail: message.Email,
            recipientName: message.FullName,
            subject: "Nova senha temporária da plataforma Quiver",
            body: BuildTemporaryPasswordBody(message),
            filePrefix: "temporary-password",
            slug: message.ScopeLabel,
            cancellationToken);

    private async Task<IdentityEmailDeliveryResult> SendAsync(
        string recipientEmail,
        string recipientName,
        string subject,
        string body,
        string filePrefix,
        string slug,
        CancellationToken cancellationToken)
    {
        var mode = (_options.Mode ?? "File").Trim();

        if (mode.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return new IdentityEmailDeliveryResult("Disabled", false, null);
        }

        if (mode.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            await SendBySmtpAsync(recipientEmail, recipientName, subject, body, cancellationToken);
            return new IdentityEmailDeliveryResult("Smtp", true, null);
        }

        var outboxPath = await SaveToOutboxAsync(filePrefix, recipientEmail, slug, subject, body, cancellationToken);
        return new IdentityEmailDeliveryResult("File", true, outboxPath);
    }

    private async Task<string> SaveToOutboxAsync(
        string filePrefix,
        string recipientEmail,
        string slug,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var outboxDirectory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(_options.OutboxDirectory) ? "temp/email-outbox" : _options.OutboxDirectory!,
            Directory.GetCurrentDirectory());

        Directory.CreateDirectory(outboxDirectory);

        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{filePrefix}_{SanitizeFileName(recipientEmail)}_{SanitizeFileName(slug)}.txt";
        var filePath = Path.Combine(outboxDirectory, fileName);
        var content = $"Assunto: {subject}{Environment.NewLine}{Environment.NewLine}{body}";
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken);
        return filePath;
    }

    private async Task SendBySmtpAsync(
        string recipientEmail,
        string recipientName,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            throw new InvalidOperationException("O host SMTP não foi configurado.");
        }

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        mailMessage.To.Add(new MailAddress(recipientEmail, recipientName));

        using var smtpClient = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.SmtpUseSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.SmtpUsername))
        {
            smtpClient.Credentials = new NetworkCredential(_options.SmtpUsername, _options.SmtpPassword);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await smtpClient.SendMailAsync(mailMessage, cancellationToken);
    }

    private string BuildInvitationBody(InvitationEmailMessage message)
    {
        var lines = new[]
        {
            $"Olá, {message.FullName}.",
            string.Empty,
            $"Você foi convidado para acessar a escola \"{message.SchoolDisplayName}\" na plataforma Quiver.",
            $"Função: {message.RoleLabel}",
            string.Empty,
            "Use o link abaixo para criar sua senha e concluir o acesso:",
            message.InviteUrl,
            string.Empty,
            $"Este convite expira em {message.ExpiresAtUtc:dd/MM/yyyy HH:mm} UTC.",
            string.Empty,
            "Se você não esperava este convite, ignore este e-mail."
        };

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildPasswordResetBody(PasswordResetEmailMessage message)
    {
        var lines = new[]
        {
            $"Olá, {message.FullName}.",
            string.Empty,
            "Recebemos um pedido para redefinir sua senha na plataforma Quiver.",
            "Use o link abaixo para criar uma nova senha:",
            message.ResetUrl,
            string.Empty,
            $"Este link expira em {message.ExpiresAtUtc:dd/MM/yyyy HH:mm} UTC.",
            string.Empty,
            "Se você não pediu esta redefinição, ignore este e-mail."
        };

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildTemporaryPasswordBody(TemporaryPasswordEmailMessage message)
    {
        var lines = new[]
        {
            $"Olá, {message.FullName}.",
            string.Empty,
            "Uma nova senha temporária foi gerada para o seu acesso na plataforma Quiver.",
            string.Empty,
            $"URL: {_options.PublicLoginUrl}",
            $"E-mail: {message.Email}",
            $"Senha temporária: {message.TemporaryPassword}",
            string.Empty,
            "No próximo acesso, a troca da senha continuará obrigatória.",
            string.Empty,
            "Se você não esperava este e-mail, entre em contato com a administração da escola."
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
    }
}

public sealed record InvitationEmailMessage(
    string FullName,
    string Email,
    string SchoolDisplayName,
    string SchoolSlug,
    string RoleLabel,
    string InviteUrl,
    DateTime ExpiresAtUtc);

public sealed record PasswordResetEmailMessage(
    string FullName,
    string Email,
    string ScopeLabel,
    string ResetUrl,
    DateTime ExpiresAtUtc);

public sealed record TemporaryPasswordEmailMessage(
    string FullName,
    string Email,
    string ScopeLabel,
    string TemporaryPassword);

public sealed record IdentityEmailDeliveryResult(
    string Mode,
    bool Delivered,
    string? OutboxFilePath);
