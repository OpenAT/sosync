using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using WebSosync.Common.Interfaces;

namespace WebSosync.Services
{
    public class MailService
        : IMailService, IDisposable
    {
        private SmtpClient _smtp;
        private string _sender;

        public MailService(string host, int port, string sender)
        {
            _smtp = new SmtpClient(host, port);
            _sender = sender;
        }

        public void Dispose()
        {
            _smtp.Dispose();
        }

        public void Send(string commaSeparatedRecipients, string subject, string body)
        {
            var msg = new MailMessage() {
                From = new MailAddress(_sender),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            msg.To.Add(commaSeparatedRecipients);
            _smtp.Send(msg);
        }
    }
}
