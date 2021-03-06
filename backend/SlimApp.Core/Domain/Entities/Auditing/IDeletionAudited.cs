namespace SlimApp.Domain.Entities.Auditing
{
    public static partial class EntityAuditingHelper
    {
        /// <summary>
        /// This interface is implemented by entities which wanted to store deletion information (who and when deleted).
        /// </summary>
        public interface IDeletionAudited : IHasDeletionTime
        {
            /// <summary>
            /// Which user deleted this entity?
            /// </summary>
            long? DeleterUserId { get; set; }
        }
    }
}
