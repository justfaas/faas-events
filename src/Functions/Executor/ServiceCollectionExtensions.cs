internal static class FunctionExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddFunctionExecutor( this IServiceCollection services, Action<FunctionExecutorOptions> configure )
    {
        services.AddTransient<IFunctionExecutor, FunctionExecutor>()
            .AddHttpClient()
            .Configure( configure );

        return ( services );
    }
}
