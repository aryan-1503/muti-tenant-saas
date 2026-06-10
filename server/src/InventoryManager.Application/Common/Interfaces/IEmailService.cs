namespace InventoryManager.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default);
    Task SendLowStockAlertAsync(string toEmail, string toName, string productName, string warehouseName, decimal currentQty, decimal reorderPoint, CancellationToken ct = default);
    Task SendInviteAsync(string toEmail, string orgName, string inviteUrl, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string toName, string resetUrl, CancellationToken ct = default);
}
