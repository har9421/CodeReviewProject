public class TestClass
{
    public async Task doAsync()
    {
        var unusedVariable = "This variable is not used";
        await Task.Delay(10);
    }
}