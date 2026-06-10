using InventoryManager.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace InventoryManager.Infrastructure.Services;

/// <summary>
/// Sends transactional emails via SMTP using MailKit.
/// Credentials are stored in appsettings (or user secrets for development).
/// All methods catch exceptions and log — email failure must never crash the application.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _config["Email:FromName"] ?? "Inventory Manager",
                _config["Email:FromAddress"] ?? string.Empty));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _config["Email:SmtpHost"] ?? string.Empty,
                int.Parse(_config["Email:SmtpPort"] ?? "587"),
                SecureSocketOptions.StartTls,
                ct);
            await client.AuthenticateAsync(
                _config["Email:Username"] ?? string.Empty,
                _config["Email:Password"] ?? string.Empty,
                ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} with subject {Subject}", toEmail, subject);
            // Do NOT rethrow — email failure must not crash the business operation
        }
    }

    public async Task SendLowStockAlertAsync(string toEmail, string toName, string productName,
        string warehouseName, decimal currentQty, decimal reorderPoint, CancellationToken ct = default)
    {
        var subject = $"⚠️ Low Stock Alert: {productName}";
        var body = $"""
            <h2>Low Stock Alert</h2>
            <p>Hi {toName},</p>
            <p>The following item has fallen at or below its reorder point:</p>
            <table style="border-collapse:collapse;width:100%;max-width:500px">
              <tr><td><strong>Product</strong></td><td>{productName}</td></tr>
              <tr><td><strong>Location</strong></td><td>{warehouseName}</td></tr>
              <tr><td><strong>Current Qty</strong></td><td style="color:#e53e3e"><strong>{currentQty}</strong></td></tr>
              <tr><td><strong>Reorder Point</strong></td><td>{reorderPoint}</td></tr>
            </table>
            <p style="margin-top:24px"><a href="#" style="background:#3b82f6;color:white;padding:10px 20px;border-radius:6px;text-decoration:none">Create Purchase Order</a></p>
            <p style="color:#888;font-size:12px">This alert was sent by your Inventory Manager system.</p>
            """;
        await SendAsync(toEmail, toName, subject, body, ct);
    }

    public async Task SendInviteAsync(string toEmail, string orgName, string inviteUrl, CancellationToken ct = default)
    {
        var subject = $"You've been invited to join {orgName} on Inventory Manager";
        var body = $"""
            <h2>You've been invited!</h2>
            <p>You have been invited to join <strong>{orgName}</strong> on Inventory Manager.</p>
            <p>Click the button below to set up your account:</p>
            <p><a href="{inviteUrl}" style="background:#3b82f6;color:white;padding:10px 20px;border-radius:6px;text-decoration:none">Accept Invitation</a></p>
            <p style="color:#888;font-size:12px">This invitation expires in 48 hours. If you didn't expect this, you can ignore it.</p>
            """;
        await SendAsync(toEmail, "Team Member", subject, body, ct);
    }

    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetUrl, CancellationToken ct = default)
    {
        var subject = "Reset your Inventory Manager password";
        var body = $"""
            <h2>Password Reset</h2>
            <p>Hi {toName},</p>
            <p>We received a request to reset your password. Click below to choose a new one:</p>
            <p><a href="{resetUrl}" style="background:#3b82f6;color:white;padding:10px 20px;border-radius:6px;text-decoration:none">Reset Password</a></p>
            <p style="color:#888;font-size:12px">This link expires in 1 hour. If you didn't request a reset, ignore this email.</p>
            """;
        await SendAsync(toEmail, toName, subject, body, ct);
    }
}
