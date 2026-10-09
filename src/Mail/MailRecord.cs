namespace AgrariaOS.Mail;

// Stored in the farmer's modData, alongside the save that owns the rewards.
internal sealed class MailRecord
{
    public MailRecord() { }
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string MailId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Date { get; set; } = "";
    public int DateOrder { get; set; }
    public bool Updates { get; set; }
    public bool Pending { get; set; }
    public bool Historical { get; set; }
    public bool HasItemCommand { get; set; }
    public List<MailAttachment> Attachments { get; set; } = new();
}

internal sealed class MailAttachment
{
    public MailAttachment() { }
    public string ItemId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public int Remaining { get; set; }
    public string ItemXml { get; set; } = "";
}
