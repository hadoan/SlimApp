namespace SlimApp.Domain.Entities.Auditing
{
    public static partial class EntityAuditingHelper
    {
        /// <summary>
        /// Adds navigation properties to <see cref="IDeletionAudited"/> interface for user.
        /// </summary>
        /// <typeparam name="TUser">Type of the user</typeparam>
        public interface IDeletionAudited<TUser> : IDeletionAudited
            where TUser : IEntity<long>
        {
            /// <summary>
            /// Reference to the deleter user of this entity.
            /// </summary>
            TUser DeleterUser { get; set; }
        }
    }
}
