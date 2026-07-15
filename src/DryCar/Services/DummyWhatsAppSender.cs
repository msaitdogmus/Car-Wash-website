using System.Threading.Tasks;

namespace DryCar.Services;

public class DummyWhatsAppSender : IWhatsAppSender
{
    public Task SendAsync(string phoneE164, string message)
    {
        return Task.CompletedTask;
    }
}
