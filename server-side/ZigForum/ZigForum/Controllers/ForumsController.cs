﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.ModelBinding;
using ZigForum.Controllers.Common;
using ZigForum.Models;
using ZigForum.Models.Validators;
using ZigForum.Models.DTOs;

namespace ZigForum.Controllers
{
    [Authorize(Roles = "Administrator")]
    [RoutePrefix("api/forums")]
    public class ForumsController : ZigForumApiController
    {
        public ForumsController() : base() { }

        public ForumsController(ApplicationDbContext db) : base(db) { }

        [AllowAnonymous]
        [HttpGet]
        [Route("")]
        public async Task<IHttpActionResult> Get(int page = 1, int pageSize = 15)
        {
            var data = await Db.Forums
                .OrderByDescending(f => f.Name)
                .Select(f => new ForumDTO
                {
                    Id = f.Id,
                    Name = f.Name,
                    Created = f.Created
                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize).ToArrayAsync();

            return Ok(data);
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("toplevel")]
        public async Task<IHttpActionResult> GetAllTopLevel(int page = 1, int pageSize = 15)
        {
            var data = await Db.Forums
                .Where(f => !f.ParentId.HasValue)
                .Take(pageSize)
                .Select(f => new ForumDTO
                {
                    Id = f.Id,
                    Name = f.Name
                })
                .OrderBy(f => f.Name)
                .Skip((page - 1) * pageSize).ToListAsync();

            foreach (var f in data)
            {
                var subForums = await Db.Forums
                    .Where(sf => sf.ParentId.HasValue ? sf.ParentId.Value == f.Id : false)
                    .Select(sf => new ForumDTO
                    {
                        Id = sf.Id,
                        Name = sf.Name
                    }).ToListAsync();

                f.SubForums = subForums.Count > 0 ? subForums : null;
            }

            return Ok(data);
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("{id:int}")]
        public async Task<IHttpActionResult> GetById(int id)
        {
            var forum = await Db.Forums.FindAsync(id);

            if (forum == null)
                return NotFound();

            var data = new ForumDTO
            {
                Id = forum.Id,
                Name = forum.Name,
                Created = forum.Created
            };

            if (forum.ParentId.HasValue)
            {
                data.Parent = new ForumDTO
                {
                    Id = forum.Parent.Id,
                    Name = forum.Parent.Name
                };
            }

            if (forum.SubForums.Count > 0)
            {
                data.SubForums = forum.SubForums.Select(f => new ForumDTO
                {
                    Id = f.Id,
                    Name = f.Name
                }).ToList();
            }

            return Ok(data);
        }

        [HttpPost]
        [Route("create")]
        public async Task<IHttpActionResult> CreateNew([FromBody]ForumDTO data)
        {
            var validator = new ForumCreateNewValidator();
            var validationResult = await validator.ValidateAsync(data);

            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);

                return BadRequest(ModelState);
            }

            var forum = new Forum
            {
                ParentId = data.ParentId,
                Name = data.Name,
                Created = DateTime.Now
            };

            var storedForum = Db.Forums.Add(forum);
            await Db.SaveChangesAsync();

            return Ok(storedForum.Id);
        }

        [HttpPatch]
        [Route("{id}/edit")]
        public async Task<IHttpActionResult> Edit(int id, [FromBody]ForumDTO data)
        {
            var validator = new ForumEditValidator();
            var validationResult = await validator.ValidateAsync(data);

            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);

                return BadRequest(ModelState);
            }

            var forum = await Db.Forums.FindAsync(id);

            if (forum == null)
                return NotFound();

            forum.ParentId = data.ParentId;
            forum.Name = data.Name;

            Db.Forums.Attach(forum);

            var entry = Db.Entry<Forum>(forum);
            entry.State = EntityState.Modified;

            await Db.SaveChangesAsync();

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpDelete]
        [Route("{id:int}/delete")]
        public async Task<IHttpActionResult> Delete(int id)
        {
            var forum = await Db.Forums.FindAsync(id);

            if (forum == null)
                return NotFound();

            Db.Forums.Remove(forum);

            await Db.SaveChangesAsync();

            return StatusCode(HttpStatusCode.NoContent);
        }
    }
}
