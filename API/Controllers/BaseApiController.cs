using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BaseApiController : ControllerBase
    {
        private IMediator _medaitor;

        protected IMediator Mediator => _medaitor ??= HttpContext.RequestServices.GetService<IMediator>();
    }
}
