using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Data.Entity.Validation;
using Domain.Repository;

namespace Domain.DataAccess.Repositories
{
    /// <summary>
    /// Implementation of the IEFRepository using the Entity Framework
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public class EFRepository<TEntity> : IEFRepository<TEntity> where TEntity : class
    {
        private DbContext _dbContext;

        public EFRepository(DbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public TEntity SingleOrDefault(Expression<Func<TEntity, bool>> predicate)
        {
            return _dbContext.Set<TEntity>().Where(predicate).SingleOrDefault();
        }

        public IQueryable<TEntity> GetAll()
        {

            return _dbContext.Set<TEntity>();
  
        }

        public IQueryable<TEntity> Find(Expression<Func<TEntity, bool>> predicate)
        {
            return GetAll().Where(predicate);
        }

        public IQueryable<TEntity> FindGraph(Expression<Func<TEntity, bool>> predicate, params Expression<Func<TEntity, object>>[] path)
        {
            var query = GetAll().Where(predicate);

            foreach (var child in path)
            {
                query = query.Include(child);
            }
            return query;
        }

        public TEntity Add(TEntity entity)
        {
            var dbSet = _dbContext.Set<TEntity>();
            dbSet.Add(entity);

            return entity;
        }

        public TEntity Update(TEntity entity)
        {
            var dbSet = this.Attach(entity);
            _dbContext.Entry<TEntity>(entity).State = EntityState.Modified;     

            return entity;
        }

        public void Delete(TEntity entity)
        {
            var dbSet = this.Attach(entity);
            dbSet.Remove(entity);
        }

        public void Delete(Expression<Func<TEntity, bool>> predicate)
        {
            var dbSet = _dbContext.Set<TEntity>().Where(predicate).FirstOrDefault();
            if (dbSet != null)
            {
                _dbContext.Entry<TEntity>(dbSet).State = System.Data.Entity.EntityState.Deleted;
            }
        }

        /// <summary>
        /// Attaches the the given entity to the current context.
        /// </summary>
        /// <param name="entity">The entity to attach.</param>
        DbSet Attach(TEntity entity)
        {
            var dbSet = _dbContext.Set<TEntity>();
            if (_dbContext.Entry(entity).State == EntityState.Detached)
            {
                dbSet.Attach(entity);
            }

            return dbSet;
        }

    }
}
