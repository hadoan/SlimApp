using SlimApp.Domain.Uow;

namespace SlimApp.EntityFramework
{
    public class SlimAppEfDbContextInitializationContext
    {
        public IUnitOfWork UnitOfWork { get; }

        public SlimAppEfDbContextInitializationContext(IUnitOfWork unitOfWork)
        {
            UnitOfWork = unitOfWork;
        }
    }
}