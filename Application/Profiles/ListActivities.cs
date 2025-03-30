using Application.Core;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence;

namespace Application.Profiles
{
    public class ListActivities
    {
        public class Query : IRequest<Result<List<UserActivityDto>>>
        {
            public string Username { get; set; } // The username of the user whose activities we want to list
            public string Predicate { get; set; } // The predicate to filter activities (e.g., "past", "future", "hosting")
        }
        public class Handler : IRequestHandler<Query, Result<List<UserActivityDto>>>
        {
            private readonly DataContext _context;
            private readonly IMapper _mapper;

            public Handler(DataContext context, IMapper mapper)
            {
                _context = context;
                _mapper = mapper;
            }
            public async Task<Result<List<UserActivityDto>>> Handle(Query request, CancellationToken cancellationToken)
            {
                var query=_context.ActivitiesAttendees
                    .Where(u=>u.AppUser.UserName==request.Username)
                    .OrderBy(a=>a.Activity.Date)
                    .ProjectTo<UserActivityDto>(_mapper.ConfigurationProvider)
                    .AsQueryable();

                query = request.Predicate switch
                {
                    "past" => query.Where(a => a.Date <= DateTime.Now),
                    "hosting" => query.Where(a => a.HostUsername == request.Username),
                    _ => query.Where(a => a.Date >= DateTime.Now),
                };

                var activities = await query.ToListAsync();

                return Result<List<UserActivityDto>>.Success(activities);
            }
        }
    }
}
