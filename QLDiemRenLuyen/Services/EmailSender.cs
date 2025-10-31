using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QLDiemRenLuyen.Services
{
    public class EmailOptions
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 587;

        public bool EnableSsl { get; set; } = true;

        public string SenderAddress { get; set; } = string.Empty;

        public string SenderName { get; set; } = "QLDiemRenLuyen";

        public string? Username { get; set; }

        public string? Password { get; set; }

    }

    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string htmlContent, CancellationToken cancellationToken = default);
    }

    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _options;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _options = options.Value ?? new EmailOptions();
            _logger = logger;
        }

        public async Task SendAsync(string toEmail, string subject, string htmlContent, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                throw new ArgumentException("Địa chỉ email không hợp lệ", nameof(toEmail));
            }

            if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.SenderAddress))
            {
                _logger.LogWarning("Bỏ qua gửi email vì chưa cấu hình SMTP host hoặc địa chỉ người gửi.");
                return;
            }

            using var message = new MailMessage
            {
                From = new MailAddress(_options.SenderAddress, _options.SenderName),
                Subject = subject,
                Body = htmlContent,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail));

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                client.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }
            else
            {
                client.UseDefaultCredentials = false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await client.SendMailAsync(message);
                _logger.LogInformation("Đã gửi email tới {Recipient} với tiêu đề {Subject}", toEmail, subject);
            }
            catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
            {
                _logger.LogError(ex, "Không thể gửi email tới {Recipient}", toEmail);
                throw;
            }
        }
    }
}
