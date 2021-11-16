﻿using BugLab.Business.Extensions;
using BugLab.Business.Helpers;
using BugLab.Data;
using BugLab.Shared.Enums;
using BugLab.Shared.Responses;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BugLab.Business.Queries.Projects
{
    public class GetProjectsHandler : IRequestHandler<GetProjectsQuery, PagedList<ProjectResponse>>
    {
        private readonly AppDbContext _context;

        public GetProjectsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PagedList<ProjectResponse>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Projects.AsNoTracking().Where(x => x.Users.Any(u => u.Id == request.UserId));
            int totalItems;
            (query, totalItems) = await query.PaginateAsync(request.PageNumber, request.PageSize, cancellationToken);

            var bugsCounts = await _context.Bugs.AsNoTracking()
                .Where(b => query.Select(p => p.Id).Contains(b.ProjectId))
                .GroupBy(b => b.ProjectId, (key, bugs) => new
                {
                    ProjectId = key,
                    Total = bugs.Count(),
                    HighPrioritized = bugs.Count(x => x.Priority == BugPriority.High)
                }).ToListAsync(cancellationToken);

            var projects = await query.ProjectToType<ProjectResponse>().OrderBy(p => p.Title).ToListAsync(cancellationToken);
            projects = projects.Select(p =>
            {
                var bugsCount = bugsCounts.FirstOrDefault(x => x.ProjectId == p.Id);
                if (bugsCount == null) return p;

                p.TotalBugs = bugsCount.Total;
                p.TotalHighPriorityBugs = bugsCount.HighPrioritized;

                return p;
            }).ToList();

            return new PagedList<ProjectResponse>(projects, request.PageNumber, request.PageSize, totalItems);
        }
    }
}