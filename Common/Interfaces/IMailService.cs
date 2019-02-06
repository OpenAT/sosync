using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Common.Interfaces
{
    public interface IMailService
    {
        void Send(string commaSeparatedRecipients, string subject, string body);
    }
}
