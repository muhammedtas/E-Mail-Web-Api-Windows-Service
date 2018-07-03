using System;
using System.Collections.Generic;

namespace EmailWebApi.Model
{
    public class EMailModel 
    {
        public int Id { get; set; }
        public string SmtpServer { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Cc { get; set; }
        public string Bcc { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public DateTime? SentDate { get; set; }
        public bool IsSent { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadingDate { get; set; }
        public int Retry { get; set; }
        public DateTime? LastTryDate { get; set; }
        public string Exception { get; set; }
        public List<EmailAttachment> EmailAttachments { get; set; }
    }
}