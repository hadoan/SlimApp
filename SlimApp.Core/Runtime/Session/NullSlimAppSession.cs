using Abp.Runtime.Remoting;
using SlimApp.Configuration.Startup;
using SlimApp.MultiTenancy;
using SlimApp.Runtime.Remoting;

namespace SlimApp.Runtime.Session
{

    /// <summary>
    /// Implements null object pattern for <see cref="ISlimAppSession"/>.
    /// </summary>
    public class NullSlimAppSession : SlimAppSessionBase
    {
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static NullSlimAppSession Instance { get; } = new NullSlimAppSession();

        /// <inheritdoc/>
        public override long? UserId => null;

        /// <inheritdoc/>
        public override int? TenantId => null;

        public override MultiTenancySides MultiTenancySide => MultiTenancySides.Tenant;

        public override long? ImpersonatorUserId => null;

        public override int? ImpersonatorTenantId => null;

        private NullSlimAppSession() 
            : base(
                  new MultiTenancyConfig(), 
                  new DataContextAmbientScopeProvider<SessionOverride>(new AsyncLocalAmbientDataContext())
            )
        {

        }
    }
}
