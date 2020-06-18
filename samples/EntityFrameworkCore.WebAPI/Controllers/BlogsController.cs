﻿using EntityFrameworkCore.Data.Repositories.Interfaces;
using EntityFrameworkCore.Models;
using EntityFrameworkCore.QueryBuilder.Interfaces;
using EntityFrameworkCore.Repository.Extensions;
using EntityFrameworkCore.UnitOfWork.Factories;
using EntityFrameworkCore.UnitOfWork.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;

namespace EntityFrameworkCore.WebAPI.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class BlogsController : Controller
    {
        private IUnitOfWork _unitOfWork;

        public BlogsController(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

        // GET: api/Blogs
        [HttpGet]
        public async Task<IActionResult> GetBlogs(int? pageIndex = null, int? pageSize = null)
        {
            if (pageIndex <= 0 || pageSize <= 0)
            {
                return BadRequest();
            }

            var repository = _unitOfWork.Repository<Blog>();

            // Example: Paging
            var query = repository.MultipleResultQuery()
                                  .Page(pageIndex, pageSize)
                                  .Include(source => source.Include(blog => blog.Type).Include(blog => blog.Posts).ThenInclude(post => post.Comments)) as IMultipleResultQuery<Blog>;

            if (query.Paging.IsEnabled)
            {
                // Example: PagedList
                var blogs = await repository.SearchAsync(query)
                                            .ToPagedListAsync(query.Paging.PageIndex, query.Paging.PageSize, query.Paging.TotalCount);

                return Ok(blogs);
            }
            else
            {
                var blogs = await repository.SearchAsync(query);

                return Ok(blogs);
            }
        }

        // GET: api/Blogs
        [HttpGet("Search", Name = "GetBlogsByTerm")]
        public async Task<IActionResult> GetBlogsByTerm(string term)
        {
            var repository = _unitOfWork.Repository<Blog>();

            IList<Blog> blogs;

            // Example: From SQL
            blogs = await Task.FromResult(repository.FromSql($"SELECT [Id], [Url], [Title], [TypeId] FROM [dbo].[Blog] WHERE [Title] LIKE '%{term}%';"));

            // Example: Filtering
            var query = repository.MultipleResultQuery()
                                  .AndFilter(blog => blog.Title.Contains(term));

            blogs = await repository.SearchAsync(query);

            return Ok(blogs);
        }

        // GET: api/Blogs/Urls
        [HttpGet("Urls")]
        public async Task<IActionResult> GetUrls()
        {
            // Example: Custom Repository
            var repository = _unitOfWork.CustomRepository<ICustomBlogRepository>();

            var urls = await repository.GetAllBlogUrlsAsync();

            return Ok(urls);
        }

        // GET: api/Blogs/5
        [HttpGet("{id}", Name = "GetBlogById")]
        public async Task<IActionResult> GetBlogById(int id)
        {
            var repository = _unitOfWork.Repository<Blog>();

            var query = repository.SingleResultQuery()
                                  .AndFilter(blog => blog.Id == id)
                                  .Include(source => source.Include(blog => blog.Type).Include(blog => blog.Posts).ThenInclude(post => post.Comments));

            var blogResult = await repository.SingleOrDefaultAsync(query);

            return Ok(blogResult);
        }

        // POST: api/Blogs
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Blog model)
        {
            var repository = _unitOfWork.Repository<Blog>();

            await repository.AddAsync(model);

            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/Blogs/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Blog model)
        {
            var repository = _unitOfWork.Repository<Blog>();

            if (!await repository.AnyAsync(blog => blog.Id == id))
            {
                return Conflict();
            }

            repository.Update(model);

            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/Blogs/5
        [HttpPut("{id}/Title", Name = "UpdateTitle")]
        public async Task<IActionResult> Put(int id, [FromBody] string title)
        {
            var repository = _unitOfWork.Repository<Blog>();

            if (!await repository.AnyAsync(blog => blog.Id == id))
            {
                return Conflict();
            }

            // Example: TransactionScope
            using (var transactionScope = TransactionScopeFactory.CreateTransactionScope(transactionScopeAsyncFlowOption: TransactionScopeAsyncFlowOption.Enabled))
            {
                await repository.ExecuteSqlCommandAsync($"UPDATE [dbo].[Blog] SET [Title] = '{title}' WHERE [Id] = {id};");

                transactionScope.Complete();
            }

            // Example: IDbContextTransaction
            await _unitOfWork.BeginTransactionAsync();

            await _unitOfWork.ExecuteSqlCommandAsync($"UPDATE [dbo].[Blog] SET [Title] = '{title}' WHERE [Id] = {id};");

            _unitOfWork.Commit();

            return NoContent();
        }

        // DELETE: api/Blogs/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var repository = _unitOfWork.Repository<Blog>();

            if (!await repository.AnyAsync(blog => blog.Id == id))
            {
                return Conflict();
            }

            repository.Remove(x => x.Id == id);

            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }

        #region IDisposable Members

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_unitOfWork != null)
                    {
                        _unitOfWork.Dispose();
                        _unitOfWork = null;
                    }
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        #endregion IDisposable Members
    }
}