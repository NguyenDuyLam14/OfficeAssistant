namespace OfficeAssistant.API.Services.AI;

public sealed record FieldHint(
    string FieldId,
    string Context,
    int OriginalLength);