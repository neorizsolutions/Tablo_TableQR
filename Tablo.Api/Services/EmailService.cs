using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public EmailService(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public Task SendEmailAsync(string to, string subject, string body)
            => SendEmailWithResultAsync(to, subject, body);

        public async Task<EmailDispatchResult> SendEmailWithResultAsync(
            string to,
            string subject,
            string body,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            var localSinkDir = _configuration["Email:LocalSinkDirectory"];
            var host = _configuration["Email:SmtpHost"];
            var portString = _configuration["Email:SmtpPort"];
            var username = _configuration["Email:SmtpUsername"];
            var password = _configuration["Email:SmtpPassword"];
            var fromEmail = _configuration["Email:FromEmail"];
            var fromName = _configuration["Email:FromName"];
            var apiKey = _configuration["Email:BrevoApiKey"];

            if (!string.IsNullOrWhiteSpace(localSinkDir))
            {
                Directory.CreateDirectory(localSinkDir);
                var safeKey = string.IsNullOrWhiteSpace(idempotencyKey)
                    ? Guid.NewGuid().ToString("N")
                    : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey))).ToLowerInvariant();
                var jsonPath = Path.Combine(localSinkDir, $"mail_{safeKey}.json");

                if (!string.IsNullOrWhiteSpace(idempotencyKey) && File.Exists(jsonPath))
                {
                    return new EmailDispatchResult($"local-idempotent:{safeKey}");
                }

                var messageId = $"local:{safeKey}";
                var payload = JsonSerializer.Serialize(new
                {
                    messageId,
                    idempotencyKey,
                    fromEmail = fromEmail ?? "noreply@tablo.local",
                    fromName = fromName ?? "Tablo",
                    to,
                    subject,
                    body,
                    capturedAtUtc = DateTime.UtcNow
                }, new JsonSerializerOptions { WriteIndented = true });

                await File.WriteAllTextAsync(jsonPath, payload, cancellationToken);
                return new EmailDispatchResult(messageId);
            }

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                if (string.IsNullOrWhiteSpace(fromEmail))
                    throw new InvalidOperationException("The Brevo sender address is not configured.");

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                request.Headers.Add("api-key", apiKey);
                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    request.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
                }

                request.Content = JsonContent.Create(new
                {
                    sender = new { email = fromEmail, name = fromName ?? "Tablo" },
                    to = new[] { new { email = to } },
                    subject,
                    textContent = body,
                    headers = string.IsNullOrWhiteSpace(idempotencyKey)
                        ? null
                        : new Dictionary<string, string> { ["X-Idempotency-Key"] = idempotencyKey }
                });
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                string? providerMessageId = null;
                try
                {
                    using var doc = await JsonDocument.ParseAsync(
                        await response.Content.ReadAsStreamAsync(cancellationToken),
                        cancellationToken: cancellationToken);
                    if (doc.RootElement.TryGetProperty("messageId", out var prop))
                    {
                        providerMessageId = prop.GetString();
                    }
                }
                catch
                {
                    // Provider accepted the message cleanly even if response body had no JSON messageId.
                }

                return new EmailDispatchResult(providerMessageId);
            }

            if (string.IsNullOrEmpty(host))
            {
                throw new InvalidOperationException("Email delivery is not configured.");
            }

            if (!int.TryParse(portString, out int port))
            {
                port = 587;
            }

            bool enableSsl = !bool.TryParse(_configuration["Email:SmtpEnableSsl"], out var parsedSsl) || parsedSsl;
            if (enableSsl && (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)))
            {
                throw new InvalidOperationException("Email delivery is not configured.");
            }

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl
            };
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                client.Credentials = new NetworkCredential(username, password);
            }

            var messageGuid = string.IsNullOrWhiteSpace(idempotencyKey)
                ? Guid.NewGuid().ToString("N")
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey))).ToLowerInvariant();
            var smtpMessageId = $"<{messageGuid}@notify.neorizsolutions.com>";

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail ?? "noreply@tablo.local", fromName ?? "Tablo"),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            mailMessage.Headers.Add("Message-ID", smtpMessageId);
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                mailMessage.Headers.Add("X-Idempotency-Key", idempotencyKey);
            }
            mailMessage.To.Add(to);
            await client.SendMailAsync(mailMessage, cancellationToken);
            return new EmailDispatchResult(smtpMessageId);
        }
    }
}
