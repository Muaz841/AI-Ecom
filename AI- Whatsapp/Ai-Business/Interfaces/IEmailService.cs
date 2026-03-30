using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IEmailService
{
    Task SendOtpEmailAsync(
        string toEmail,
        string toName,
        string otpCode,
        int expiryMinutes,
        CancellationToken cancellationToken = default);
}
