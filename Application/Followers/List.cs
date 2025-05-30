﻿using Application.Core;
using Application.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence;

namespace Application.Followers
{
    public class List
    {
        public class Query : IRequest<Result<List<Profiles.Profile>>>
        {
            public string Predicate { get; set; } // followers or following
            public string Username { get; set; }
        }
        public class Handler : IRequestHandler<Query, Result<List<Profiles.Profile>>>
        {
            private readonly IMapper _mapper;
            private readonly DataContext _context;
            private readonly IUserAccessor _userAccessor;

            public Handler(IMapper mapper, DataContext context,IUserAccessor userAccessor )
            {
                _mapper = mapper;
                _context = context;
                _userAccessor = userAccessor;
            }
            public async Task<Result<List<Profiles.Profile>>> Handle(Query request, CancellationToken cancellationToken)
            {
                var profiles = new List<Profiles.Profile>();

                switch (request.Predicate)
                {
                    case "followers":
                        profiles = await _context.UsersFollowings.Where(x => x.Target.UserName == request.Username)
                            .Select(u => u.Observer)
                            .ProjectTo<Profiles.Profile>(_mapper.ConfigurationProvider,
                            new { currentUsername = _userAccessor.GetUsername()})
                            .ToListAsync();
                        break;
                    case "following":
                        profiles = await _context.UsersFollowings.Where(x => x.Observer.UserName == request.Username)
                            .Select(u => u.Target)
                            .ProjectTo<Profiles.Profile>(_mapper.ConfigurationProvider,
                            new { currentUsername = _userAccessor.GetUsername() })
                            .ToListAsync();
                        break;
                }

                return Result<List<Profiles.Profile>>.Success(profiles);
            }
        }
    }
}
