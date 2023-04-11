public interface IFunctionExecutor
{
    Task ExecuteAsync( Event faasEvent, CancellationToken cancellationToken );
}
