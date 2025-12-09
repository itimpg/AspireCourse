namespace Contracts
{
    public record QuestionUpdated(
        string QuestionId,
        string Title,
        string Content,
        DateTime Created,
        List<string> Tags);
}
