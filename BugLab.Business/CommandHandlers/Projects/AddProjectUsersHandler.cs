﻿using BugLab.Business.Helpers;
using BugLab.Data;
using BugLab.Shared.Commands;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BugLab.Business.CommandHandlers.Projects
{
    public class AddProjectUsersHandler : IRequestHandler<AddProjectUsersCommand>
    {
        private readonly AppDbContext _context;

        public AddProjectUsersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(AddProjectUsersCommand request, CancellationToken cancellationToken)
        {
            // TODO: refactor to a join table instead and do include, theninclude or use ref by id so wont need to have the nav property on project.
            var usersAndProject = await _context.Projects.Where(x => x.Id == request.ProjectId)
                .Select(x => new { x.Users, Project = x })
                .FirstOrDefaultAsync(cancellationToken);

            Guard.NotFound(usersAndProject, "project", request.ProjectId);

            var usersToAdd = await _context.Users.Where(x => request.UserIds.Contains(x.Id)).ToListAsync(cancellationToken);
            Guard.NotFound(usersToAdd, nameof(usersToAdd));

            IReadOnlyCollection<IdentityUser> notAlreadyMembersUsers = usersToAdd.Where(u => !usersAndProject.Users.Any(pu => pu.Id == u.Id)).ToList();

            usersAndProject.Project.Users.AddRange(notAlreadyMembersUsers);
            await _context.SaveChangesAsync(cancellationToken);

            if (notAlreadyMembersUsers.Count != usersToAdd.Count)
            {
                throw new InvalidOperationException("Some users were not added because they could not be found or they are already members of this project");
            }

            return Unit.Value;
        }
    }
}
