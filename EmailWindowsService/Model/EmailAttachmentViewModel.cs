namespace EmailWindowsService.Model
{
    public class EMailAttachmentViewModel
    {
        public int EmailId { get; set; }
        public string FileName { get; set; }
        public byte[] File { get; set; }
    }
}
