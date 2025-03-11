using Application.Core;
using Application.Interfaces;
using Domain;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Persistence;

namespace Application.Photos
{
    public class Add
    {
        public class Command : IRequest<Result<Photo>>
        {
            public string File { get; set; }
        }

        public class Handler : IRequestHandler<Command, Result<Photo>>
        {
            private readonly DataContext _context;
            private readonly IPhotoAccessor _photoAccessor;
            private readonly IUserAccessor _userAccessor;

            public Handler(DataContext context, IPhotoAccessor photoAccessor, IUserAccessor userAccessor)
            {
                _context = context ?? throw new ArgumentNullException(nameof(context));
                _photoAccessor = photoAccessor ?? throw new ArgumentNullException(nameof(photoAccessor));
                _userAccessor = userAccessor ?? throw new ArgumentNullException(nameof(userAccessor));
            }

            public async Task<Result<Photo>> Handle(Command request, CancellationToken cancellationToken)
            {
                try
                {
                    var user = await _context.Users
                        .Include(p => p.Photos)
                        .FirstOrDefaultAsync(x => x.UserName == _userAccessor.GetUsername());

                    if (user == null) return Result<Photo>.Failure("User not found");

                    // Validate the base64 string
                    if (string.IsNullOrEmpty(request.File))
                    {
                        return Result<Photo>.Failure("Base64 string is null or empty.");
                    }

                    // Decode the base64 string to a byte array
                    var base64 = request.File;
                    string base64Data;

                    if (base64.Contains(','))
                    {
                        base64Data = base64.Split(',')[1]; // Remove the data URL prefix if present
                    }
                    else
                    {
                        base64Data = base64; // Use the entire string as base64 data
                    }

                    if (string.IsNullOrEmpty(base64Data))
                    {
                        return Result<Photo>.Failure("Base64 data is null or empty.");
                    }

                    byte[] bytes;

                    try
                    {
                        bytes = Convert.FromBase64String(base64Data);
                    }
                    catch (FormatException)
                    {
                        return Result<Photo>.Failure("Invalid base64 string.");
                    }

                    // Validate file size (e.g., 10 MB limit)
                    if (bytes.Length > 10 * 1024 * 1024)
                    {
                        return Result<Photo>.Failure("File size exceeds the limit.");
                    }

                    // Convert the byte array to an IFormFile
                    var stream = new MemoryStream(bytes);
                    var file = new FormFile(stream, 0, bytes.Length, "file", "uploadedPhoto.jpg")
                    {
                        Headers = new HeaderDictionary(),
                        ContentType = "image/jpeg" // Adjust based on the file type
                    };

                    var photoUploadResult = await _photoAccessor.AddPhoto(file);

                    if (photoUploadResult == null)
                    {
                        return Result<Photo>.Failure("Failed to upload photo.");
                    }

                    var photo = new Photo
                    {
                        Url = photoUploadResult.Url,
                        Id = photoUploadResult.PublicId
                    };

                    if (!user.Photos.Any(x => x.IsMain)) photo.IsMain = true;

                    user.Photos.Add(photo);

                    var result = await _context.SaveChangesAsync() > 0;

                    if (result) return Result<Photo>.Success(photo);

                    return Result<Photo>.Failure("Problem adding photo");
                }
                catch (Exception ex)
                {
                    // Log the error
                    Console.WriteLine($"Error in Add.Handle: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    return Result<Photo>.Failure($"Internal server error: {ex.Message}");
                }
            }
        }


    }
}
