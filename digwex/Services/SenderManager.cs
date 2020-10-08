using MimeKit;
using MailKit.Net.Smtp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Digwex.Services
{
  public class SendManager
  {
    private string _email = "dddoc.info@gmail.com";
    private string _password = "fL41jo9C";

    public bool SendMessage(string email, string subject, string text)
    {
      try {
        var emailMessage = new MimeMessage();

        emailMessage.From.Add(new MailboxAddress("Администрация сайта", _email));
        emailMessage.To.Add(new MailboxAddress("", email));
        emailMessage.Subject = subject;
        emailMessage.Body = new TextPart(MimeKit.Text.TextFormat.Html) {
          Text = text
        };

        using (var client = new SmtpClient()) {
          client.Connect("smtp.gmail.com", 587, false);
          client.Authenticate(_email, _password);
          client.Send(emailMessage);
          client.Disconnect(true);
        }
      }
      catch (Exception ex) {
        Console.WriteLine(ex);
      }
      return true;
    }

  }
}
