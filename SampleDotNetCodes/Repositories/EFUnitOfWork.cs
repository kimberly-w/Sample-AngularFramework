using System;
using System.Data.Entity;
using System.Reflection;
using System.Data.Entity.Validation;
using Domain.Repository;
using Common;

namespace Domain.DataAccess.Repositories
{
    /// <summary>
    /// Inplementation of <see cref="IUnitOfWork"/> for Entity Framework.
    /// </summary>
    public class EFUnitOfWork<TContext> : IUnitOfWork
        where TContext : DbContext, new()
    {
        private TContext _dbContext;

        /// <summary>
        /// Create an instance of underlying DbContext
        /// </summary>
        public EFUnitOfWork()
        {
            _dbContext = new TContext();
            _dbContext.Configuration.LazyLoadingEnabled = false;
            _dbContext.Configuration.AutoDetectChangesEnabled = false;
            _dbContext.Configuration.ProxyCreationEnabled = false;
        }

        public IEFRepository<TEntity> CreateEFRepository<TEntity>()
            where TEntity : class
        {
            return new Repositories.EFRepository<TEntity>(_dbContext);
        }

        /// <summary>
        /// Commits the dbContext changes.
        /// </summary>
        public void Commit()
        {
            try
            {
                _dbContext.SaveChanges();
            }
            catch (Exception ex)
            {
                // log the error in cases that consumers do not have a try...catch block 
                //if ((ex) is DbEntityValidationException)
                //    logger.Error(ex.Message, ex.InnerException);

                throw ex;
            }
        }

        /// <summary>
        /// Release DbContext resources. 
        /// </summary>
        public void Dispose()
        {
            _dbContext.Dispose();
        }
    }
}
