using System.Threading.Tasks;

namespace Tablo.Api.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
}
