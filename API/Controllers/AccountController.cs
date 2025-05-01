using API.DTOs;
using API.Services;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace API.Controllers
{
    
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly TokenService _tokenService;
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        public AccountController(UserManager<AppUser> userManager,TokenService tokenService,IConfiguration config) 
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _config = config;
            _httpClient = new HttpClient
            {
                BaseAddress = new System.Uri("https://graph.facebook.com")
            };

        }

        private UserDto CreateUserObject(AppUser user)
        {
            return new UserDto
            {
                DisplayName = user.DisplayName,
                Image = user?.Photos?.FirstOrDefault(x => x.IsMain)?.Url,
                Token = _tokenService.CreateToken(user),
                Username = user.UserName
            };
        }



        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
        {
            var user =await _userManager.Users.Include(p=>p.Photos)
                .FirstOrDefaultAsync(x=>x.Email== loginDto.Email);

            if(user== null) return Unauthorized();

            var result=await _userManager.CheckPasswordAsync(user,loginDto.Password);
            if(result)
            {
                await SetRefreshToken(user);
                return CreateUserObject(user);
            }
            return Unauthorized();
        }

      
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
        {
            if(await _userManager.Users.AnyAsync(x=>x.UserName==registerDto.Username))
            {
                ModelState.AddModelError("username", "Username taken");
                return ValidationProblem();
            }

            if (await _userManager.Users.AnyAsync(x => x.Email == registerDto.Email))
            {
                ModelState.AddModelError("email", "Email taken");
                return ValidationProblem();
            }

            var user = new AppUser
            {
                DisplayName = registerDto.DisplayName,
                Email = registerDto.Email,
                UserName = registerDto.Username
            };
            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (result.Succeeded)
            {
                await SetRefreshToken(user);
                return CreateUserObject(user);

            }
            return BadRequest(result.Errors);
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<UserDto>> GetCurrentUser()
        {
            var user = await _userManager.Users.Include(p => p.Photos).FirstOrDefaultAsync(x => x.Email == User.FindFirstValue(ClaimTypes.Email));
            await SetRefreshToken(user);
            return CreateUserObject(user);

        }

        [AllowAnonymous]
        [HttpPost("fbLogin")]
        public async Task<ActionResult<UserDto>> FacebookLogin(string accessToken)
        {
          var fbVerifyKeys = _config["Facebook:AppId"]+ "|" + _config["Facebook:ApiSecret"];
            var verifyTokenResponse = await _httpClient.GetAsync($"/debug_token?input_token={accessToken}&access_token={fbVerifyKeys}");

            if(!verifyTokenResponse.IsSuccessStatusCode)
            {
                return Unauthorized();
            }
            var fbUrl = $"me?access_token={accessToken}&fields=name,email,picture.width(100).height(100)";

            var fbInfo = await _httpClient.GetFromJsonAsync<FacebookDto>(fbUrl);
            var user=await _userManager.Users
                .Include(p => p.Photos)
                .FirstOrDefaultAsync(x => x.Email == fbInfo.Email);

            if(user!=null) return CreateUserObject(user);
            user=new AppUser
            {
                DisplayName = fbInfo.Name,
                Email = fbInfo.Email,
                UserName = fbInfo.Email,
                Photos = new List<Photo>
                {
                    new Photo
                    {
                        Id="fb_"+fbInfo.Id,
                        Url = fbInfo.Picture.Data.Url,
                        IsMain = true
                    }
                }
            };
            var result= await _userManager.CreateAsync(user);
            if(!result.Succeeded)
            {
                return BadRequest("Problem creating user account");
            }
            await SetRefreshToken(user);
            return CreateUserObject(user);
        }

        [Authorize]
        [HttpPost("refreshToken")]
        public async Task<ActionResult<UserDto>> RefreshToken()
        {
            Console.WriteLine($"Incoming cookies: {string.Join(", ", Request.Cookies.Keys)}");
            var refreshToken = Request.Cookies["refreshToken"];
            var user = await _userManager.Users
                .Include(r=>r.RefreshTokens)
                .Include(p=>p.Photos)
                .FirstOrDefaultAsync(x=>x.UserName==User.FindFirstValue(ClaimTypes.Name));
            
            if (user == null) return Unauthorized(new ProblemDetails { Title = "User not found" });

            var oldToken = user.RefreshTokens.SingleOrDefault(x => x.Token == refreshToken);

            if (oldToken != null && !oldToken.IsActive) return Unauthorized();

            if(oldToken !=null) oldToken.Revoked = DateTime.UtcNow;

            return CreateUserObject(user);
        }

        //[AllowAnonymous]
        //[HttpPost("refreshToken")]
        //public async Task<ActionResult<UserDto>> RefreshToken()
        //{
        //    Console.WriteLine($"Incoming cookies: {string.Join(", ", Request.Cookies.Keys)}");
        //    var refreshToken = Request.Cookies["refreshToken"];
        //    Console.WriteLine($"RefreshToken cookie: {refreshToken}");

        //    if (string.IsNullOrEmpty(refreshToken))
        //    {
        //        Console.WriteLine("No refresh token provided");
        //        return Unauthorized(new ProblemDetails { Title = "No refresh token provided" });
        //    }

        //    // Find user by refresh token
        //    var user = await _userManager.Users
        //        .Include(r => r.RefreshTokens)
        //        .Include(p => p.Photos)
        //        .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == refreshToken));

        //    if (user == null)
        //    {
        //        Console.WriteLine($"User not found for refresh token: {refreshToken}");
        //        return Unauthorized(new ProblemDetails { Title = "User not found" });
        //    }

        //    var oldToken = user.RefreshTokens.SingleOrDefault(x => x.Token == refreshToken);

        //    if (oldToken == null)
        //    {
        //        Console.WriteLine($"Refresh token not found in user's RefreshTokens: {refreshToken}");
        //        return Unauthorized(new ProblemDetails { Title = "Invalid refresh token" });
        //    }

        //    if (!oldToken.IsActive)
        //    {
        //        Console.WriteLine($"Inactive refresh token: {refreshToken}, Expires: {oldToken.Expires}, Revoked: {oldToken.Revoked}");
        //        return Unauthorized(new ProblemDetails { Title = "Inactive refresh token" });
        //    }

        //    // Revoke the old refresh token
        //    oldToken.Revoked = DateTime.UtcNow;

        //    // Generate and set a new refresh token
        //    await SetRefreshToken(user);
        //    await _userManager.UpdateAsync(user);

        //    return CreateUserObject(user);
        //}



        private async Task SetRefreshToken(AppUser user)
        {
            var refreshToken = _tokenService.GenerateRefreshToken();

            user.RefreshTokens.ToList().RemoveAll(t => !t.IsActive);
            user.RefreshTokens.Add(refreshToken);
            await _userManager.UpdateAsync(user);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(7),
                Secure = true, // Temporarily disable for debugging
                SameSite = SameSiteMode.Lax,
                Path = "/"

            };

            Response.Cookies.Append("refreshToken", refreshToken.Token, cookieOptions);

        }
    }
}
