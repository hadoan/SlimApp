namespace SlimApp.EntityFramework
{
    public interface IShouldInitializeDcontext
    {
        void Initialize(AbpEfDbContextInitializationContext initializationContext);
    }
}