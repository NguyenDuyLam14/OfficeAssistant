namespace OfficeAssistant.API.DTOs.Email;

/// <summary>Yêu cầu gợi ý trả lời. Tone: formal | friendly | short.</summary>
public sealed class SuggestReplyRequest
{
    public string Tone { get; set; } = "formal";
    public string? Note { get; set; }
}

/// <summary>Yêu cầu gửi một email đơn.</summary>
public sealed class SendEmailRequest
{
    public string To { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>ID email Gmail đang trả lời (nếu là thư trả lời).</summary>
    public string? ReplyToMessageId { get; set; }
}

public sealed class RecipientInput
{
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public Dictionary<string, string> Fields { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CreateCampaignRequest
{
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public List<RecipientInput> Recipients { get; set; } = new();
}

public sealed class PreviewCampaignRequest
{
    public string Subject { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public RecipientInput? Recipient { get; set; }
}

public sealed record ParsedRecipients(
    List<string> Variables,
    List<RecipientInput> Recipients,
    int TotalRows,
    int Invalid,
    int Duplicates,
    bool Truncated);
