namespace SlimApp.EntityFramework
{
    public interface IShouldInitializeDcontext
    {
        void Initialize(SlimAppEfDbContextInitializationContext initializationContext);
    }
}