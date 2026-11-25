using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;
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

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var host = _configuration["Email:SmtpHost"];
            var portString = _configuration["Email:SmtpPort"];
            var username = _configuration["Email:SmtpUsername"];
            var password = _configuration["Email:SmtpPassword"];
            var fromEmail = _configuration["Email:FromEmail"];
            var fromName = _configuration["Email:FromName"];
            var apiKey = _configuration["Email:BrevoApiKey"];

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                if (string.IsNullOrWhiteSpace(fromEmail))
                    throw new InvalidOperationException("The Brevo sender address is not configured.");

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                request.Headers.Add("api-key", apiKey);
                request.Content = JsonContent.Create(new
                {
                    sender = new { email = fromEmail, name = fromName ?? "Tablo" },
                    to = new[] { new { email = to } },
                    subject,
                    textContent = body
                });
                using var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return;
            }

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("Email delivery is not configured.");
            }

            if (!int.TryParse(portString, out int port))
            {
                port = 587;
            }

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail ?? "noreply@tablo.local", fromName ?? "Tablo"),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            mailMessage.To.Add(to);
            await client.SendMailAsync(mailMessage);
        }
    }
}
