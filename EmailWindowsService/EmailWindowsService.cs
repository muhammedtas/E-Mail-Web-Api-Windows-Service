using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web.Http;
using EmailWindowsService.Model;
using YourProjectName.DataServices;
using YourProjectName.Domain.Entities;
using YourProjectName.Domain.Interfaces;
using YourProjectName.Infrastructure;
using YourProjectName.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Timer = System.Timers.Timer;

namespace EmailWindowsService
{

    public delegate Task DontStopAsync();
    
    public partial class EmailWindowsService : ServiceBase
    {
        public static readonly YourProjectNameDbContext Db = new YourProjectNameDbContext();
        public readonly IEmail EmailService = new EmailService(Db);
        public readonly IEmailAttachment EmailAttachmentService = new EmailAttachmentService(Db);
        private readonly int _timerInterval = int.Parse(ConfigurationManager.AppSettings["timerInterval"]);
        private readonly int _retryLimitation = int.Parse(ConfigurationManager.AppSettings["retryLimitation"]);
        private readonly string _apiUrl = ConfigurationManager.AppSettings["apiUrl"];
        private readonly Timer _timer = new Timer();
        private List<Email> _emails = new List<Email>();
        private List<EmailAttachment> _emailAttachments = new List<EmailAttachment>();
        private const string BaseAddress = "EmailWebApi.Controllers.MailController";

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly CancellationToken _cancellationToken;
        public EmailWindowsService()
        {
            InitializeComponent();
            #region Write Log
            DateTime dtEntry = DateTime.Now;
            const string address = BaseAddress + ".MailController(IEmail emailService)";
            var dic = LogBusiness.GetDictionary();
            dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
            var dicParams = LogBusiness.GetDictionary();
            dicParams.Add(LogFieldName.Token, dicParams);
            dic.Add(LogFieldName.TimeElapsed, ((int)(DateTime.Now - dtEntry).TotalMilliseconds).ToString());
            LogBusiness.CustomLog("AnonymousUser", LogEvent.MethodStart, dic, dicParams);
            _cancellationToken = _cts.Token;
            #endregion
        }

        public void DebugStart(bool immediate)
        {
            OnStart(immediate ? new[] { "immediate" } : null);
        }

        protected override void OnStart(string[] args)
        {
            _timer.Enabled = true;
            _timer.Interval = _timerInterval;
            _timer.Elapsed += _timer_Elapsed;
            if (args == null || !args.Contains("immediate")) return;
            DontStopAsync dtAsync = WorkerAsync;
            Task.Factory.StartNew(dtAsync.Invoke, _cancellationToken, TaskCreationOptions.LongRunning,
                    TaskScheduler.Current)
                .ConfigureAwait(false);
        }
        
        protected override void OnStop()
        {
            _timer.Stop();
            _timer.Dispose();
        }
        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
       
            DontStopAsync dtAsync = WorkerAsync;
            Task.Factory.StartNew(dtAsync.Invoke, _cancellationToken, TaskCreationOptions.LongRunning,
                    TaskScheduler.Current)
                .ConfigureAwait(false);
            
        }
        
        [HttpPost]
        private async Task WorkerAsync()
        {

            _timer.Stop();

            string fromMail = ""; 

            try
            {
                _emails = EmailService.GetAll().Where(x => !x.IsSent).OrderBy(x => x.Id).ToList();

                if (_emails.Count == 0){ _timer.Start(); return;}

                foreach (var email in _emails)
                {
                    using (var client = new HttpClient())
                    {
                        client.BaseAddress = new Uri(_apiUrl);

                        using (var stream = new MemoryStream())
                        using (var bson = new BsonWriter(stream))
                        {
                            var jsonSerializer = new JsonSerializer();

                            fromMail = email.From; 

                            _emailAttachments = EmailAttachmentService.GetAllNoTracking().Where(x => x.EmailId == email.Id).ToList();

                            var attachments = _emailAttachments.Select(attachment => new EMailAttachmentViewModel
                            {
                                File = attachment.File,
                                FileName = attachment.FileName,
                                EmailId = attachment.EmailId
                            }).ToList();

                            if (email.Retry >= _retryLimitation) continue;
                            var willBeSentAgainEmail = new EMailViewModel
                            {
                                Id = email.Id,
                                From = email.From,
                                To = email.To,
                                Subject = email.Subject,
                                Body = email.Body,
                                Bcc = email.Bcc,
                                Cc = email.Cc,
                                Exception = email.Exception,
                                Retry = ++email.Retry,
                                SmtpServer = email.SmtpServer,
                                IsRead = false,
                                IsSent = false,
                                LastTryDate = DateTime.Now,
                                EmailAttachments = attachments
                            };

                            jsonSerializer.Serialize(bson, willBeSentAgainEmail);

                            client.DefaultRequestHeaders.Accept.Clear();
                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/bson"));

                            var byteArrayContent = new ByteArrayContent(stream.ToArray());
                            byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("application/bson");

                           await client?.PostAsync("api/mail/sendemailasync", byteArrayContent, _cancellationToken);
                            
                        }
                    }
                }
                _timer.Start();
            }
            catch (Exception ex)
            {
                _timer.Start();
                #region Write Log
                string address = BaseAddress + ".MailController(IEmail emailService)";
                var dic = LogBusiness.GetDictionary();
                dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
                var dicParams = LogBusiness.GetDictionary();
                dicParams.Add(LogFieldName.Token, dicParams);
                dic = LogBusiness.GetDictionary();
                dic.Add(LogFieldName.FullyQualifiedFunctionName, address);
                dic.Add(LogFieldName.ErrorMessage, ex.Message);
                dic.Add(LogFieldName.StackTrace, ex.StackTrace);
                LogBusiness.CustomLog(fromMail, LogEvent.ErrorEvent, dic, dicParams);
                #endregion

            }
            _timer.Start();
        }

        
    }
}






















