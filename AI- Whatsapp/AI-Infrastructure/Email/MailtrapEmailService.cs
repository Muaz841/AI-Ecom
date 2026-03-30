using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EcomAI.Platform.Infrastructure.Email;

public sealed class MailtrapEmailService : IEmailService
{
    private readonly MailtrapSettings _settings;
    private readonly IApplicationLogger _logger;

    public MailtrapEmailService(IOptions<MailtrapSettings> settings, IApplicationLogger logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendOtpEmailAsync(
        string toEmail,
        string toName,
        string otpCode,
        int expiryMinutes,
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = "Your Password Reset Code";

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = BuildOtpHtml(otpCode, expiryMinutes),
            TextBody = $"Your password reset code is: {otpCode}. It expires in {expiryMinutes} minutes. Do not share this code."
        };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            _logger.Info("OTP email dispatched to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to send OTP email to {Email}", toEmail);
            throw;
        }
    }

    private static string BuildOtpHtml(string otpCode, int expiryMinutes) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="UTF-8" /><meta name="viewport" content="width=device-width,initial-scale=1" /></head>
        <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f4;padding:40px 0;">
            <tr>
              <td align="center">
                <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                  <tr>
                    <td style="background:#1a73e8;padding:28px 40px;">
                      <h1 style="margin:0;color:#ffffff;font-size:20px;font-weight:600;">AutoBaat Platform</h1>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:36px 40px 24px;">
                      <h2 style="margin:0 0 12px;color:#202124;font-size:22px;">Password Reset Request</h2>
                      <p style="margin:0 0 24px;color:#5f6368;font-size:15px;line-height:1.6;">
                        We received a request to reset your password. Enter the code below to continue.
                        This code is valid for <strong>{expiryMinutes} minutes</strong>.
                      </p>
                      <div style="background:#f8f9fa;border:1px solid #e8eaed;border-radius:8px;padding:24px;text-align:center;margin-bottom:28px;">
                        <span style="font-size:42px;font-weight:700;letter-spacing:12px;color:#1a73e8;font-family:'Courier New',monospace;">{otpCode}</span>
                      </div>
                      <p style="margin:0;color:#80868b;font-size:13px;line-height:1.6;">
                        If you did not request a password reset, you can safely ignore this email.
                        Do not share this code with anyone.
                      </p>
                    </td>
                  </tr>
                  <tr>
                    <td style="background:#f8f9fa;padding:16px 40px;border-top:1px solid #e8eaed;">
                      <p style="margin:0;color:#80868b;font-size:12px;">
                        &copy; {DateTime.UtcNow.Year} AutoBaat Platform. All rights reserved.
                      </p>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
}
