using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SlimApp.Collections.Extensions;
using SlimApp.Configuration.Startup;
using SlimApp.Dependency;
using SlimApp.Domain.Entities;
using SlimApp.Domain.Entities.Auditing;
using SlimApp.Domain.Repositories;
using SlimApp.Domain.Uow;
using SlimApp.EntityFrameworkCore;
using SlimApp.Events.Bus;
using SlimApp.Events.Bus.Entities;
using SlimApp.Extensions;
using SlimApp.Runtime.Session;
using SlimApp.Timing;

namespace SlimApp.EntityFramework
{
    /// <summary>
    /// Base class for all DbContext classes in the application.
    /// </summary>
    public abstract class SlimAppDbContext : DbContext, ITransientDependency, IShouldInitialize, IShouldInitializeDcontext
    {
        /// <summary>
        /// Used to get current session values.
        /// </summary>
        public ISlimAppSession SlimAppSession { get; set; }

        /// <summary>
        /// Used to trigger entity change events.
        /// </summary>
        public IEntityChangeEventHelper EntityChangeEventHelper { get; set; }

        /// <summary>
        /// Reference to the logger.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Reference to the event bus.
        /// </summary>
        public IEventBus EventBus { get; set; }

        /// <summary>
        /// Reference to GUID generator.
        /// </summary>
        public IGuidGenerator GuidGenerator { get; set; }

        /// <summary>
        /// Reference to the current UOW provider.
        /// </summary>
        public ICurrentUnitOfWorkProvider CurrentUnitOfWorkProvider { get; set; }

        /// <summary>
        /// Reference to multi tenancy configuration.
        /// </summary>
        public IMultiTenancyConfig MultiTenancyConfig { get; set; }

        /// <summary>
        /// Can be used to suppress automatically setting TenantId on SaveChanges.
        /// Default: false.
        /// </summary>
        public bool SuppressAutoSetTenantId { get; set; }

        /// <summary>
        /// Constructor.
        /// Uses <see cref="ISlimAppStartupConfiguration.DefaultNameOrConnectionString"/> as connection string.
        /// </summary>
        protected SlimAppDbContext()
        {
            InitializeDbContext();
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        protected SlimAppDbContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
            InitializeDbContext();
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        protected SlimAppDbContext(DbCompiledModel model)
            : base(model)
        {
            InitializeDbContext();
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        protected SlimAppDbContext(DbConnection existingConnection, bool contextOwnsConnection)
            : base(existingConnection, contextOwnsConnection)
        {
            InitializeDbContext();
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        protected SlimAppDbContext(string nameOrConnectionString, DbCompiledModel model)
            : base(nameOrConnectionString, model)
        {
            InitializeDbContext();
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        protected SlimAppDbContext(ObjectContext objectContext, bool dbContextOwnsObjectContext)
            : base(objectContext, dbContextOwnsObjectContext)
        {
            InitializeDbContext();
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        protected SlimAppDbContext(DbConnection existingConnection, DbCompiledModel model, bool contextOwnsConnection)
            : base(existingConnection, model, contextOwnsConnection)
        {
            InitializeDbContext();
        }

        private void InitializeDbContext()
        {
            SetNullsForInjectedProperties();
            RegisterToChanges();
        }

        private void RegisterToChanges()
        {
            ((IObjectContextAdapter)this)
                .ObjectContext
                .ObjectStateManager
                .ObjectStateManagerChanged += ObjectStateManager_ObjectStateManagerChanged;
        }

        protected virtual void ObjectStateManager_ObjectStateManagerChanged(object sender, CollectionChangeEventArgs e)
        {
            var contextAdapter = (IObjectContextAdapter)this;
            if (e.Action != CollectionChangeAction.Add)
            {
                return;
            }

            var entry = contextAdapter.ObjectContext.ObjectStateManager.GetObjectStateEntry(e.Element);
            switch (entry.State)
            {
                case EntityState.Added:
                    CheckAndSetId(entry.Entity);
                    CheckAndSetMustHaveTenantIdProperty(entry.Entity);
                    SetCreationAuditProperties(entry.Entity, GetAuditUserId());
                    break;
                    //case EntityState.Deleted: //It's not going here at all
                    //    SetDeletionAuditProperties(entry.Entity, GetAuditUserId());
                    //    break;
            }
        }

        private void SetNullsForInjectedProperties()
        {
            Logger = NullLogger.Instance;
            SlimAppSession = NullSlimAppSession.Instance;
            EntityChangeEventHelper = NullEntityChangeEventHelper.Instance;
            GuidGenerator = SequentialGuidGenerator.Instance;
            EventBus = NullEventBus.Instance;
        }

        public virtual void Initialize()
        {
            Database.Initialize(false);
            this.SetFilterScopedParameterValue(SlimAppDataFilters.MustHaveTenant, SlimAppDataFilters.Parameters.TenantId,
                SlimAppSession.TenantId ?? 0);
            this.SetFilterScopedParameterValue(SlimAppDataFilters.MayHaveTenant, SlimAppDataFilters.Parameters.TenantId,
                SlimAppSession.TenantId);
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            ConfigureFilters(modelBuilder);
        }

        protected virtual void ConfigureFilters(DbModelBuilder modelBuilder)
        {
            modelBuilder.Filter(SlimAppDataFilters.SoftDelete, (ISoftDelete d) => d.IsDeleted, false);
            modelBuilder.Filter(SlimAppDataFilters.MustHaveTenant,
#pragma warning disable CS0472 // The result of the expression is always the same since a value of this type is never equal to 'null'
                (IMustHaveTenant t, int tenantId) => t.TenantId == tenantId || (int?)t.TenantId == null,
#pragma warning restore CS0472 // While "(int?)t.TenantId == null" seems wrong, it's needed. See https://github.com/jcachat/EntityFramework.DynamicFilters/issues/62#issuecomment-208198058
                0);
            modelBuilder.Filter(SlimAppDataFilters.MayHaveTenant,
                (IMayHaveTenant t, int? tenantId) => t.TenantId == tenantId, 0);
        }

        public override int SaveChanges()
        {
            try
            {
                var changedEntities = ApplySlimAppConcepts();
                var result = base.SaveChanges();
                EntityChangeEventHelper.TriggerEvents(changedEntities);
                return result;
            }
            catch (DbEntityValidationException ex)
            {
                LogDbEntityValidationException(ex);
                throw;
            }
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var changeReport = ApplySlimAppConcepts();
                var result = await base.SaveChangesAsync(cancellationToken);
                await EntityChangeEventHelper.TriggerEventsAsync(changeReport);
                return result;
            }
            catch (DbEntityValidationException ex)
            {
                LogDbEntityValidationException(ex);
                throw;
            }
        }

        protected virtual EntityChangeReport ApplySlimAppConcepts()
        {
            var changeReport = new EntityChangeReport();

            var userId = GetAuditUserId();

            foreach (var entry in ChangeTracker.Entries().ToList())
            {
                ApplySlimAppConcepts(entry, userId, changeReport);
            }

            return changeReport;
        }

        public virtual void Initialize(SlimAppEfDbContextInitializationContext initializationContext)
        {
            var uowOptions = initializationContext.UnitOfWork.Options;
            if (uowOptions.Timeout.HasValue && !Database.CommandTimeout.HasValue)
            {
                Database.CommandTimeout = uowOptions.Timeout.Value.TotalSeconds.To<int>();
            }

            if (Clock.SupportsMultipleTimezone)
            {
                ((IObjectContextAdapter)this).ObjectContext.ObjectMaterialized += (sender, args) =>
                {
                    var entityType = ObjectContext.GetObjectType(args.Entity.GetType());

                    Configuration.AutoDetectChangesEnabled = false;
                    var previousState = Entry(args.Entity).State;

                    DateTimePropertyInfoHelper.NormalizeDatePropertyKinds(args.Entity, entityType);

                    Entry(args.Entity).State = previousState;
                    Configuration.AutoDetectChangesEnabled = true;
                };
            }
        }

        protected virtual void ApplySlimAppConcepts(DbEntityEntry entry, long? userId, EntityChangeReport changeReport)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    ApplySlimAppConceptsForAddedEntity(entry, userId, changeReport);
                    break;
                case EntityState.Modified:
                    ApplySlimAppConceptsForModifiedEntity(entry, userId, changeReport);
                    break;
                case EntityState.Deleted:
                    ApplySlimAppConceptsForDeletedEntity(entry, userId, changeReport);
                    break;
            }

            AddDomainEvents(changeReport.DomainEvents, entry.Entity);
        }

        protected virtual void ApplySlimAppConceptsForAddedEntity(DbEntityEntry entry, long? userId,
            EntityChangeReport changeReport)
        {
            CheckAndSetId(entry.Entity);
            CheckAndSetMustHaveTenantIdProperty(entry.Entity);
            CheckAndSetMayHaveTenantIdProperty(entry.Entity);
            SetCreationAuditProperties(entry.Entity, userId);
            changeReport.ChangedEntities.Add(new EntityChangeEntry(entry.Entity, EntityChangeType.Created));
        }

        protected virtual void ApplySlimAppConceptsForModifiedEntity(DbEntityEntry entry, long? userId,
            EntityChangeReport changeReport)
        {
            SetModificationAuditProperties(entry.Entity, userId);

            if (entry.Entity is ISoftDelete && entry.Entity.As<ISoftDelete>().IsDeleted)
            {
                SetDeletionAuditProperties(entry.Entity, userId);
                changeReport.ChangedEntities.Add(new EntityChangeEntry(entry.Entity, EntityChangeType.Deleted));
            }
            else
            {
                changeReport.ChangedEntities.Add(new EntityChangeEntry(entry.Entity, EntityChangeType.Updated));
            }
        }

        protected virtual void ApplySlimAppConceptsForDeletedEntity(DbEntityEntry entry, long? userId,
            EntityChangeReport changeReport)
        {
            if (IsHardDeleteEntity(entry))
            {
                changeReport.ChangedEntities.Add(new EntityChangeEntry(entry.Entity, EntityChangeType.Deleted));
                return;
            }

            CancelDeletionForSoftDelete(entry);
            SetDeletionAuditProperties(entry.Entity, userId);
            changeReport.ChangedEntities.Add(new EntityChangeEntry(entry.Entity, EntityChangeType.Deleted));
        }

        protected virtual bool IsHardDeleteEntity(DbEntityEntry entry)
        {
            if (!EntityHelper.IsEntity(entry.Entity.GetType()))
            {
                return false;
            }

            if (CurrentUnitOfWorkProvider?.Current?.Items == null)
            {
                return false;
            }

            if (!CurrentUnitOfWorkProvider.Current.Items.ContainsKey(UnitOfWorkExtensionDataTypes.HardDelete))
            {
                return false;
            }

            var hardDeleteItems = CurrentUnitOfWorkProvider.Current.Items[UnitOfWorkExtensionDataTypes.HardDelete];
            if (!(hardDeleteItems is HashSet<string> objects))
            {
                return false;
            }

            var currentTenantId = GetCurrentTenantIdOrNull();
            var hardDeleteKey = EntityHelper.GetHardDeleteKey(entry.Entity, currentTenantId);
            return objects.Contains(hardDeleteKey);
        }

        protected virtual void AddDomainEvents(List<DomainEventEntry> domainEvents, object entityAsObj)
        {
            var generatesDomainEventsEntity = entityAsObj as IGeneratesDomainEvents;
            if (generatesDomainEventsEntity == null)
            {
                return;
            }

            if (generatesDomainEventsEntity.DomainEvents.IsNullOrEmpty())
            {
                return;
            }

            domainEvents.AddRange(
                generatesDomainEventsEntity.DomainEvents.Select(
                    eventData => new DomainEventEntry(entityAsObj, eventData)));
            generatesDomainEventsEntity.DomainEvents.Clear();
        }

        protected virtual void CheckAndSetId(object entityAsObj)
        {
            //Set GUID Ids
            var entity = entityAsObj as IEntity<Guid>;
            if (entity != null && entity.Id == Guid.Empty)
            {
                var entityType = ObjectContext.GetObjectType(entityAsObj.GetType());
                var idIdPropertyName = GetIdPropertyName(entityType);
                var edmProperty = GetEdmProperty(entityType, idIdPropertyName);

                if (edmProperty != null && edmProperty.StoreGeneratedPattern == StoreGeneratedPattern.None)
                {
                    entity.Id = GuidGenerator.Create();
                }
            }
        }

        EdmProperty GetEdmProperty(Type type, string propertyName)
        {
            var metadata = ((IObjectContextAdapter)this).ObjectContext.MetadataWorkspace;

            var objectItemCollection = ((ObjectItemCollection)metadata.GetItemCollection(DataSpace.OSpace));

            var entityType = metadata.GetItems<EntityType>(DataSpace.OSpace)
                .Single(t => objectItemCollection.GetClrType(t) == type);

            var entitySet = metadata.GetItems<EntityContainer>(DataSpace.SSpace).Single().EntitySets
                .Single(s => s.ElementType.Name == entityType.Name);

            return entitySet.ElementType.Properties.Single(e =>
                string.Equals(e.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        }

        string GetIdPropertyName(Type type)
        {
            var metadata = ((IObjectContextAdapter)this).ObjectContext.MetadataWorkspace;

            var objectItemCollection = ((ObjectItemCollection)metadata.GetItemCollection(DataSpace.OSpace));

            var entityType = metadata.GetItems<EntityType>(DataSpace.OSpace)
                .Single(t => objectItemCollection.GetClrType(t) == type);

            var entitySetCSpace = metadata
                .GetItems<EntityContainer>(DataSpace.CSpace)
                .Single()
                .EntitySets
                .Single(s => s.ElementType.Name == entityType.Name);

            var mapping = metadata.GetItems<EntityContainerMapping>(DataSpace.CSSpace)
                .Single()
                .EntitySetMappings
                .Single(s => s.EntitySet == entitySetCSpace);

            return mapping
                .EntityTypeMappings.Single()
                .Fragments.Single()
                .PropertyMappings
                .OfType<ScalarPropertyMapping>()
                .Single(m => m.Property.Name == nameof(Entity.Id))
                .Column
                .Name;
        }

        protected virtual void CheckAndSetMustHaveTenantIdProperty(object entityAsObj)
        {
            if (SuppressAutoSetTenantId)
            {
                return;
            }

            //Only set IMustHaveTenant entities
            if (!(entityAsObj is IMustHaveTenant))
            {
                return;
            }

            var entity = entityAsObj.As<IMustHaveTenant>();

            //Don't set if it's already set
            if (entity.TenantId != 0)
            {
                return;
            }

            var currentTenantId = GetCurrentTenantIdOrNull();

            if (currentTenantId != null)
            {
                entity.TenantId = currentTenantId.Value;
            }
            else
            {
                throw new SlimAppException("Can not set TenantId to 0 for IMustHaveTenant entities!");
            }
        }

        protected virtual void CheckAndSetMayHaveTenantIdProperty(object entityAsObj)
        {
            if (SuppressAutoSetTenantId)
            {
                return;
            }

            //Only set IMayHaveTenant entities
            if (!(entityAsObj is IMayHaveTenant))
            {
                return;
            }

            var entity = entityAsObj.As<IMayHaveTenant>();

            //Don't set if it's already set
            if (entity.TenantId != null)
            {
                return;
            }

            //Only works for single tenant applications
            if (MultiTenancyConfig?.IsEnabled ?? false)
            {
                return;
            }

            //Don't set if MayHaveTenant filter is disabled
            if (!this.IsFilterEnabled(SlimAppDataFilters.MayHaveTenant))
            {
                return;
            }

            entity.TenantId = GetCurrentTenantIdOrNull();
        }

        protected virtual void SetCreationAuditProperties(object entityAsObj, long? userId)
        {
            EntityAuditingHelper.SetCreationAuditProperties(
                MultiTenancyConfig,
                entityAsObj,
                SlimAppSession.TenantId,
                userId,
                CurrentUnitOfWorkProvider?.Current?.AuditFieldConfiguration
            );
        }

        protected virtual void SetModificationAuditProperties(object entityAsObj, long? userId)
        {
            EntityAuditingHelper.SetModificationAuditProperties(
                MultiTenancyConfig,
                entityAsObj,
                SlimAppSession.TenantId,
                userId,
                CurrentUnitOfWorkProvider?.Current?.AuditFieldConfiguration
            );
        }

        protected virtual void CancelDeletionForSoftDelete(DbEntityEntry entry)
        {
            if (!(entry.Entity is ISoftDelete))
            {
                return;
            }

            var softDeleteEntry = entry.Cast<ISoftDelete>();
            softDeleteEntry.Reload();
            softDeleteEntry.State = EntityState.Modified;
            softDeleteEntry.Entity.IsDeleted = true;
        }

        protected virtual void SetDeletionAuditProperties(object entityAsObj, long? userId)
        {
            EntityAuditingHelper.SetDeletionAuditProperties(
                MultiTenancyConfig,
                entityAsObj,
                SlimAppSession.TenantId,
                userId,
                CurrentUnitOfWorkProvider?.Current?.AuditFieldConfiguration
            );
        }

        protected virtual void LogDbEntityValidationException(DbEntityValidationException exception)
        {
            Logger.Error("There are some validation errors while saving changes in EntityFramework:");
            foreach (var ve in exception.EntityValidationErrors.SelectMany(eve => eve.ValidationErrors))
            {
                Logger.Error(" - " + ve.PropertyName + ": " + ve.ErrorMessage);
            }
        }

        protected virtual long? GetAuditUserId()
        {
            if (SlimAppSession.UserId.HasValue &&
                CurrentUnitOfWorkProvider != null &&
                CurrentUnitOfWorkProvider.Current != null &&
                CurrentUnitOfWorkProvider.Current.GetTenantId() == SlimAppSession.TenantId)
            {
                return SlimAppSession.UserId;
            }

            return null;
        }

        protected virtual int? GetCurrentTenantIdOrNull()
        {
            if (CurrentUnitOfWorkProvider?.Current != null)
            {
                return CurrentUnitOfWorkProvider.Current.GetTenantId();
            }

            return SlimAppSession.TenantId;
        }
    }
}
