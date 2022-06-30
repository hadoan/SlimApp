using SlimApp.Domain.Uow;

namespace SlimApp.EntityFramework
{
    public class AbpEfDbContextInitializationContext
    {
        public IUnitOfWork UnitOfWork { get; }

        public AbpEfDbContextInitializationContext(IUnitOfWork unitOfWork)
        {
            UnitOfWork = unitOfWork;
        }
    }
}