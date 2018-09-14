using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Web.Http;
using EmailWebApi.Model;
using YourProject.Domain.Entities;
using YourProject.Domain.Interfaces;
using YourProject.Logging;
using Attachment = System.Net.Mail.Attachment;
using EmailAttachment = YourProject.Domain.Entities.EmailAttachment;

namespace EmailWebApi.Controller
{

    public class MailController : ApiController
    {
        private readonly IEmail _emailService;

        public static int ApiRetry;

        public static string MailSmtpHost = ConfigurationManager.AppSettings["MailSmtpHost"];
        public static string MailSmtpHost2 = ConfigurationManager.AppSettings["MailSmtpHost2"];

        public static int MailSmtpPort = Convert.ToInt32(ConfigurationManager.AppSettings["MailSmtpPort"]);
        public static bool MailEnableSsl = true;
        private static string baseAddress = "EmailWebApi.Controller.MailController";

        public MailController(IEmail emailService)
        {
            #region Write Log
            var dtEntry = DateTime.Now;
            var address = baseAddress + ".MailController(IEmail emailService)";
            var dic = LogBusiness.GetDictionary();
            dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
            var dicParams = LogBusiness.GetDictionary();
            dicParams.Add(LogFieldName.Token, dicParams);
            LogBusiness.CustomLog("AnonymousUser", LogEvent.MethodStart, dic, dicParams);
            #endregion
            try
            {
                _emailService = emailService;
            }
            catch (Exception ex)
            {
                #region Write Log
                dic = LogBusiness.GetDictionary();
                dic.Add(LogFieldName.FullyQualifiedFunctionName, baseAddress);
                dic.Add(LogFieldName.ErrorMessage, ex.Message);
                dic.Add(LogFieldName.StackTrace, ex.StackTrace);
                dic.Add(LogFieldName.TimeElapsed, ((int)(DateTime.Now - dtEntry).TotalMilliseconds).ToString());
                LogBusiness.CustomLog("EMail informations could not be loaded to MailAPI possibly from unexpected error", LogEvent.ErrorEvent, dic, dicParams);
                #endregion
            }

        }
        
      
        [HttpPost]
        [Route("api/mail/sendemailasync")]
        public async void SendEMailAsync([FromBody]EMailModel model)
        {
            await Task.Run(() =>
                {
                    
                    if (model == null) return;
                    #region Write Log
                    var dtEntry = DateTime.Now;
                    var address = baseAddress + ".SendEMail([FromBody]EMailModel model)";
                    var dic = LogBusiness.GetDictionary();
                    dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
                    var dicParams = LogBusiness.GetDictionary();
                    dicParams.Add(LogFieldName.Token, dicParams);
                    LogBusiness.CustomLog(model.From, LogEvent.MethodStart, dic, dicParams);
                    #endregion
                    start:
                    using (var smtp = new SmtpClient(MailSmtpHost))
                    {
                        using (var message = new MailMessage())
                        {
                            
                            smtp.Credentials = new NetworkCredential();
                            smtp.Host = MailSmtpHost;
                            smtp.Port = MailSmtpPort;
                            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                            smtp.UseDefaultCredentials = false;
                            message.IsBodyHtml = true;

                            if (model.Retry == 0)
                            {
                                try
                                {
                                    if (File.Exists(@"/Resources/yourMailPic.gif"))
                                    {
                                     
                                        message.AlternateViews.Add(CreateEmbeddedImage("/Resources/yourMailPic.gif"));
                                       
                                    }

                                    if (!string.IsNullOrEmpty(model.From))
                                    {
                                        var fromList = model.From.Split('&');
                                        message.From = new MailAddress(fromList[0], fromList[1]);
                                    }
                                    if (!string.IsNullOrEmpty(model.Subject)) message.Subject = model.Subject;

                                    if (!string.IsNullOrEmpty(model.Body)) message.Body = model.Body;

                                    if (!string.IsNullOrEmpty(model.To))
                                    {
                                        var toList = model.To.Split(';');
                                        foreach (var toRaw in toList)
                                        {
                                            var toRawList = toRaw.Split('&');
                                            var addr = new MailAddress(toRawList[0], toRawList[1]);
                                            message.To.Add(addr);
                                        }
                                    }
                                    //CC's
                                    if (!string.IsNullOrEmpty(model.Cc))
                                    {
                                        var ccMails = model.Cc.Split(';').ToArray();

                                        foreach (var ccEmail in ccMails)
                                        {
                                            var ccRawList = ccEmail.Split('&');
                                            var ccaddr = new MailAddress(ccRawList[0], ccRawList[1]);
                                            message.CC.Add(ccaddr);
                                        }
                                    }
                                    //BCC's
                                    if (!string.IsNullOrEmpty(model.Bcc))
                                    {
                                        var bccMails = model.Bcc.Split(';').ToArray();

                                        foreach (var bccEmail in bccMails)
                                        {
                                            var bccRawList = bccEmail.Split('&');
                                            var bccaddr = new MailAddress(bccRawList[0], bccRawList[1]);
                                            message.Bcc.Add(bccaddr);
                                        }
                                    }
                                    //Attachment's
                                    if (model.EmailAttachments.Count > 0)
                                    {
                                        foreach (var attachment in model.EmailAttachments)
                                        {
                                      
                                            var attachmentMetaData = new Attachment(new MemoryStream(attachment.File),
                                                attachment.FileName, MediaTypeNames.Application.Octet);
                                            message.Attachments.Add(attachmentMetaData);
                                        }
                                    }
                                    model.SentDate = DateTime.Now;

                                    var mail = new Email
                                    {
                                        Subject = model.Subject,
                                        Bcc = model.Bcc,
                                        Cc = model.Cc,
                                        Body = model.Body,
                                        From = model.From,
                                        To = model.To,
                                        SmtpServer = MailSmtpHost
                                    };
                                    foreach (var attachment in model.EmailAttachments)
                                    {
                                        var attach =
                                            new EmailAttachment
                                            {
                                                File = attachment.File,
                                                FileName = attachment.FileName
                                            };
                                        mail.EmailAttachments.Add(attach);
                                    }
                                    _emailService.Add(mail);
                                    _emailService.Save();

                                    var savePkId = mail.Id;

                                    var updateEmail = _emailService.GetAll().FirstOrDefault(x => x.Id == savePkId);
                                    try
                                    {
                                        smtp.Send(message); 
                                        if (updateEmail == null) return;
                                        updateEmail.IsSent = true;
                                        updateEmail.SentDate = DateTime.Now;
                                        _emailService.Update(updateEmail);
                                        _emailService.Save();
                                    }
                                    catch (Exception e)
                                    {
                                        if (updateEmail != null)
                                        {
                                            updateEmail.Exception = e.InnerException == null ? e.Message : e.Message + " --> " + e.InnerException.Message;
                                            updateEmail.LastTryDate = DateTime.Now;
                                            updateEmail.Retry = updateEmail.Retry++;
                                            _emailService.Update(updateEmail);
                                            _emailService.Save();
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    #region Write Log
                                    dic = LogBusiness.GetDictionary();
                                    dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
                                    dic.Add(LogFieldName.ErrorMessage, ex.Message);
                                    dic.Add(LogFieldName.StackTrace, ex.StackTrace);
                                    LogBusiness.CustomLog(model.From, LogEvent.ErrorEvent, dic, dicParams);
                                    MailSmtpHost = MailSmtpHost2;
                                    goto start;

                                    #endregion
                                }
                                finally
                                {
                                    #region Write Log
                                    dic = LogBusiness.GetDictionary();
                                    dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
                                    dic.Add(LogFieldName.TimeElapsed, ((int)(DateTime.Now - dtEntry).TotalMilliseconds).ToString());
                                    LogBusiness.CustomLog(model.From, LogEvent.MethodEnd, dic, dicParams);
                                    #endregion
                                }
                            }
                            
                            else
                            {
                                try
                                {

                                    if (!string.IsNullOrEmpty(model.From))
                                    {
                                        var fromList = model.From.Split('&');
                                        message.From = new MailAddress(fromList[0], fromList[1]);
                                    }
                                    if (!string.IsNullOrEmpty(model.Subject))
                                    {
                                        message.Subject = model.Subject;
                                    }
                                    if (!string.IsNullOrEmpty(model.Body))
                                    {
                                        message.Body = model.Body;
                                    }

                                    if (!string.IsNullOrEmpty(model.To))
                                    {
                                        var toList = model.To.Split(';');
                                        foreach (var toRaw in toList)
                                        {
                                            var toRawList = toRaw.Split('&');
                                            var addr = new MailAddress(toRawList[0], toRawList[1]);
                                            message.To.Add(addr);
                                        }
                                    }
                                    //CC's
                                    if (!string.IsNullOrEmpty(model.Cc))
                                    {
                                        var ccMails = model.Cc.Split(';').ToArray();

                                        foreach (var ccEmail in ccMails)
                                        {
                                            var ccRawList = ccEmail.Split('&');
                                            var ccaddr = new MailAddress(ccRawList[0], ccRawList[1]);
                                            message.CC.Add(ccaddr);
                                        }
                                    }
                                    //BCC's
                                    if (!string.IsNullOrEmpty(model.Bcc))
                                    {
                                        var bccMails = model.Bcc.Split(';').ToArray();

                                        foreach (var bccEmail in bccMails)
                                        {
                                            var bccRawList = bccEmail.Split('&');
                                            var bccaddr = new MailAddress(bccRawList[0], bccRawList[1]);
                                            message.Bcc.Add(bccaddr);
                                        }
                                    }
                                    if (model.EmailAttachments.Count > 0)
                                    {
                                        foreach (var attachment in model.EmailAttachments)
                                        {
                                            var attachmentMetaData = new Attachment(new MemoryStream(attachment.File),
                                                 attachment.FileName, MediaTypeNames.Application.Octet);
                                            message.Attachments.Add(attachmentMetaData);
                                        }
                                    }

                                    var updateEmail = _emailService.GetAll().FirstOrDefault(x => x.Id == model.Id);
                                    start2:
                                    try
                                    { 
                                        smtp.Send(message); 
                                        if (updateEmail == null) return;

                                        updateEmail.SmtpServer = smtp.Host;
                                        updateEmail.Retry = model.Retry; // WindowsSerive deneme sayısını artırıyor. 
                                        updateEmail.IsSent = true;
                                        updateEmail.SentDate = DateTime.Now;
                                        updateEmail.Exception = "Last Exception was :" + model.Exception;
                                        updateEmail.LastTryDate = model.LastTryDate;
                                        _emailService.Update(updateEmail);
                                        _emailService.Save();
                                    }
                                    catch (Exception e)
                                    {
                                        if (updateEmail != null)
                                        {
                                            updateEmail.SmtpServer = smtp.Host;
                                            updateEmail.Exception = e.InnerException == null
                                                ? e.Message
                                                : e.Message + " --> " + e.InnerException.Message;
                                            updateEmail.LastTryDate = DateTime.Now;
                                            updateEmail.Retry = model.Retry;
                                            _emailService.Update(updateEmail);
                                            _emailService.Save();
                                        }
                                        smtp.Host = MailSmtpHost2;
                                        goto start2;
                                    }

                                }
                                catch (Exception ex)
                                {
                                    #region Write Log

                                    dic = LogBusiness.GetDictionary();
                                    dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
                                    dic.Add(LogFieldName.ErrorMessage, ex.Message);
                                    dic.Add(LogFieldName.StackTrace, ex.StackTrace);
                                    LogBusiness.CustomLog(model.From, LogEvent.ErrorEvent, dic, dicParams);
                                    #endregion

                                }
                                finally
                                {
                                    #region Write Log
                                    dic = LogBusiness.GetDictionary();
                                    dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
                                    dic.Add(LogFieldName.TimeElapsed, ((int)(DateTime.Now - dtEntry).TotalMilliseconds).ToString());
                                    LogBusiness.CustomLog(model.From, LogEvent.MethodEnd, dic, dicParams);
                                    #endregion
                                }
                            }
                        }
                    }
                });
        }


        [HttpPost]
        [Route("api/mail/sendemail")]
        public void SendEMail([FromBody]EMailModel model)
        {
            if (model == null) return;
            #region Write Log
            var dtEntry = DateTime.Now;
            var address = baseAddress + ".SendEMail([FromBody]EMailModel model)";
            var dic = LogBusiness.GetDictionary();
            dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
            var dicParams = LogBusiness.GetDictionary();
            dicParams.Add(LogFieldName.Token, dicParams);
            LogBusiness.CustomLog(model.From, LogEvent.MethodStart, dic, dicParams);
            #endregion
            start:
            using (var smtp = new SmtpClient(MailSmtpHost))
            {
                using (var message = new MailMessage())
                {
                    smtp.Credentials = new NetworkCredential();
                    smtp.Host = MailSmtpHost;
                    smtp.Port = MailSmtpPort;
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.UseDefaultCredentials = false;
                    message.IsBodyHtml = true;
                  
                    if (model.Retry == 0)
                    {
                        try
                        {

                            var list = new List<MailAddress>();

                            if (!string.IsNullOrEmpty(model.From))
                            {
                                var fromList = model.From.Split('&');
                                var mailFromList = new MailAddress(fromList[0], fromList[1]);
                                message.From = mailFromList;
                                list.Add(mailFromList);
                            }
                            if (!string.IsNullOrEmpty(model.Subject)) message.Subject = model.Subject;
                            
                            if (!string.IsNullOrEmpty(model.To))
                            {
                                var toList = model.To.Split(';');
                                foreach (var toRaw in toList)
                                {
                                    var toRawList = toRaw.Split('&');
                                    var mailToList = new MailAddress(toRawList[0], toRawList[1]);
                                    var addr = mailToList;
                                    message.To.Add(addr);
                                    list.Add(mailToList);
                                }
                            }
                            //CC 's
                            if (!string.IsNullOrEmpty(model.Cc))
                            {
                                var ccMails = model.Cc.Split(';').ToArray();

                                foreach (var ccEmail in ccMails)
                                {
                                    var ccRawList = ccEmail.Split('&');
                                    var mailCcList = new MailAddress(ccRawList[0], ccRawList[1]);
                                    var ccaddr = mailCcList;
                                    message.CC.Add(ccaddr);
                                    list.Add(mailCcList);
                                }
                            }
                            //BCC 's
                            if (!string.IsNullOrEmpty(model.Bcc))
                            {
                                var bccMails = model.Bcc.Split(';').ToArray();

                                foreach (var bccEmail in bccMails)
                                {
                                    var bccRawList = bccEmail.Split('&');
                                    var mailBccList = new MailAddress(bccRawList[0], bccRawList[1]);
                                    var bccaddr = mailBccList;
                                    message.Bcc.Add(bccaddr);
                                    list.Add(mailBccList);
                                }
                            }

                            EmailTemplate tempData = new EmailTemplate(list);

                           
                            if (!string.IsNullOrEmpty(model.Body)) message.CreateHtmlBody(list); else message.CreateHtmlBody(tempData);

                            //Attachment 's
                            if (model.EmailAttachments.Count > 0)
                            {
                                foreach (var attachment in model.EmailAttachments)
                                {
                                    var attachmentMetaData = new Attachment(new MemoryStream(attachment.File),
                                    attachment.FileName, MediaTypeNames.Application.Octet);
                                    message.Attachments.Add(attachmentMetaData);
                                }
                            }
                            model.SentDate = DateTime.Now;

                            var mail = new Email
                            {
                                Subject = model.Subject,
                                Bcc = model.Bcc,
                                Cc = model.Cc,
                                Body = model.Body,
                                From = model.From,
                                To = model.To,
                                SmtpServer = MailSmtpHost
                            };
                            foreach (var attachment in model.EmailAttachments)
                            {
                                var attach =
                                    new EmailAttachment
                                    {
                                        File = attachment.File,
                                        FileName = attachment.FileName
                                    };
                                mail.EmailAttachments.Add(attach);
                            }
                            _emailService.Add(mail);
                            _emailService.Save();

                            var savePkId = mail.Id;

                            var updateEmail = _emailService.GetAll().FirstOrDefault(x => x.Id == savePkId);
                            try
                            {
                                smtp.Send(message);
                                if (updateEmail == null) return;
                                updateEmail.IsSent = true;
                                updateEmail.SentDate = DateTime.Now;
                                _emailService.Update(updateEmail);
                                _emailService.Save();
                            }
                            catch (Exception e)
                            {
                                if (updateEmail != null)
                                {
                                    updateEmail.Exception = e.InnerException == null ? e.Message : e.Message + " --> " + e.InnerException.Message;
                                    updateEmail.LastTryDate = DateTime.Now;
                                    updateEmail.Retry = updateEmail.Retry++;
                                    _emailService.Update(updateEmail);
                                    _emailService.Save();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            #region Write Log
                            dic = LogBusiness.GetDictionary();
                            dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
                            dic.Add(LogFieldName.ErrorMessage, ex.Message);
                            dic.Add(LogFieldName.StackTrace, ex.StackTrace);
                            LogBusiness.CustomLog(model.From, LogEvent.ErrorEvent, dic, dicParams);
                            ApiRetry++;
                            MailSmtpHost = MailSmtpHost2;
                            if (ApiRetry<5) goto start;
                            ApiRetry = 0;

                            #endregion
                        }
                        finally
                        {
                            #region Write Log
                            dic = LogBusiness.GetDictionary();
                            dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
                            dic.Add(LogFieldName.TimeElapsed, ((int)(DateTime.Now - dtEntry).TotalMilliseconds).ToString());
                            LogBusiness.CustomLog(model.From, LogEvent.MethodEnd, dic, dicParams);
                            #endregion
                        }
                    }
                   
                    else
                    {
                        try
                        {

                            if (!string.IsNullOrEmpty(model.From))
                            {
                                var fromList = model.From.Split('&');
                                message.From = new MailAddress(fromList[0], fromList[1]);
                            }
                            if (!string.IsNullOrEmpty(model.Subject))
                            {
                                message.Subject = model.Subject;
                            }
                            if (!string.IsNullOrEmpty(model.Body))
                            {
                                message.Body = model.Body;
                            }

                            if (!string.IsNullOrEmpty(model.To))
                            {
                                var toList = model.To.Split(';');
                                foreach (var toRaw in toList)
                                {
                                    var toRawList = toRaw.Split('&');
                                    var addr = new MailAddress(toRawList[0], toRawList[1]);
                                    message.To.Add(addr);
                                }
                            }
                            //CC 's
                            if (!string.IsNullOrEmpty(model.Cc))
                            {
                                var ccMails = model.Cc.Split(';').ToArray();

                                foreach (var ccEmail in ccMails)
                                {
                                    var ccRawList = ccEmail.Split('&');
                                    var ccaddr = new MailAddress(ccRawList[0], ccRawList[1]);
                                    message.CC.Add(ccaddr);
                                }
                            }
                            //BCC 's
                            if (!string.IsNullOrEmpty(model.Bcc))
                            {
                                var bccMails = model.Bcc.Split(';').ToArray();

                                foreach (var bccEmail in bccMails)
                                {
                                    var bccRawList = bccEmail.Split('&');
                                    var bccaddr = new MailAddress(bccRawList[0], bccRawList[1]);
                                    message.Bcc.Add(bccaddr);
                                }
                            }
                            if (model.EmailAttachments.Count > 0)
                            {
                                foreach (var attachment in model.EmailAttachments)
                                {
                                    var attachmentMetaData = new Attachment(new MemoryStream(attachment.File),
                                         attachment.FileName, MediaTypeNames.Application.Octet);
                                    message.Attachments.Add(attachmentMetaData);
                                }
                            }

                            var updateEmail = _emailService.GetAll().FirstOrDefault(x => x.Id == model.Id);
                            start2:
                            try
                            {

                                smtp.Send(message);
                                if (updateEmail == null) return;

                                updateEmail.SmtpServer = smtp.Host;
                                updateEmail.Retry = model.Retry; 
                                updateEmail.IsSent = true;
                                updateEmail.Exception = "Last Exception was :" + model.Exception; 
                                updateEmail.SentDate = DateTime.Now;
                                updateEmail.LastTryDate = model.LastTryDate;
                                _emailService.Update(updateEmail);
                                _emailService.Save();
                            }
                            catch (Exception e)
                            {
                                if (updateEmail != null)
                                {
                                    updateEmail.SmtpServer = smtp.Host;
                                    updateEmail.Exception = e.InnerException == null
                                        ? e.Message
                                        : e.Message + " --> " + e.InnerException.Message;
                                    updateEmail.LastTryDate = model.LastTryDate;
                                    updateEmail.Retry = model.Retry;
                                    _emailService.Update(updateEmail);
                                    _emailService.Save();
                                }
                                smtp.Host = MailSmtpHost2;
                                goto start2;
                            }

                        }
                        catch (Exception ex)
                        {
                            #region Write Log

                            dic = LogBusiness.GetDictionary();
                            dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
                            dic.Add(LogFieldName.ErrorMessage, ex.Message);
                            dic.Add(LogFieldName.StackTrace, ex.StackTrace);
                            LogBusiness.CustomLog(model.From, LogEvent.ErrorEvent, dic, dicParams);
                            #endregion

                        }
                        finally
                        {
                            #region Write Log
                            dic = LogBusiness.GetDictionary();
                            dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
                            dic.Add(LogFieldName.TimeElapsed, ((int)(DateTime.Now - dtEntry).TotalMilliseconds).ToString());
                            LogBusiness.CustomLog(model.From, LogEvent.MethodEnd, dic, dicParams);
                            #endregion
                        }
                    }
                }
            }
        }

        
        private static AlternateView CreateEmbeddedImage(string filePath)
        {
            var hiddenImage = new LinkedResource(filePath) { ContentId = Guid.NewGuid().ToString() };
            var htmlBody = @"<img src='cid:" + hiddenImage + @"'/>";
            var alternateView = AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html);
            alternateView.LinkedResources.Add(hiddenImage);
            return alternateView;
        }


    }

}
