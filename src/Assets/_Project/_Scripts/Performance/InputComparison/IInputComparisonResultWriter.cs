namespace Shield_Shot.Performance.InputComparison
{
    public interface IInputComparisonResultWriter
    {
        string Write(InputComparisonResultDocument document);
    }
}
