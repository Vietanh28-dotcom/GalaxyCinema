using GalaxyCinema.Data;
using GalaxyCinema.Models;
using GalaxyCinema.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace GalaxyCinema.Controllers;

public class ContactController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly MailSettings _mail;

    public ContactController(ApplicationDbContext db, IOptions<MailSettings> mail)
    {
        _db = db;
        _mail = mail.Value;
    }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string name, string email, string subject, string message)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(message))
        {
            ViewBag.Error = "Vui lòng điền đầy đủ họ tên, email và nội dung.";
            return View();
        }

        var model = new ContactMessage
        {
            Name = name.Trim(),
            Email = email.Trim(),
            Subject = subject?.Trim(),
            Message = message.Trim(),
            CreatedAt = VnTime.Now
        };
        _db.ContactMessages.Add(model);
        await _db.SaveChangesAsync();

        await SendContactEmailAsync(model);

        ViewBag.Success = true;
        return View();
    }

    private async Task SendContactEmailAsync(ContactMessage msg)
    {
        if (string.IsNullOrEmpty(_mail.SmtpHost) || string.IsNullOrEmpty(_mail.Username)) return;
        try
        {
            using var client = new SmtpClient(_mail.SmtpHost, _mail.SmtpPort)
            {
                Credentials = new NetworkCredential(_mail.Username, _mail.Password),
                EnableSsl = _mail.EnableSsl
            };
            using var mailMsg = new MailMessage
            {
                From = new MailAddress(_mail.From, _mail.FromName),
                Subject = $"[Liên hệ] {msg.Subject}",
                Body = $"Tên: {msg.Name}\nEmail: {msg.Email}\nChủ đề: {msg.Subject}\n\nNội dung:\n{msg.Message}"
            };
            mailMsg.To.Add(_mail.To);
            await client.SendMailAsync(mailMsg);
        }
        catch
        {
            // email send failure silently ignored — message already saved to database
        }
    }
}