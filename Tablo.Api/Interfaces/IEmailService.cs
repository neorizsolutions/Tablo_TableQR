using System.Threading;
using System.Threading.Tasks;

namespace Tablo.Api.Interfaces
{
    public record EmailDispatchResult(string? ProviderMessageId);

    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);

        async Task<EmailDispatchResult> SendEmailWithResultAsync(
            string to,
            string subject,
            string body,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            await SendEmailAsync(to, subject, body);
            return new EmailDispatchResult(null);
        }
    }
}
