using System.Threading.Tasks;

namespace DryCar.Services;

public interface IWhatsAppSender
{
    Task SendAsync(string phoneE164, string message);
}
