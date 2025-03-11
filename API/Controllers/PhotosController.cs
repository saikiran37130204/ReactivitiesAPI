using Application.Photos;
using Microsoft.AspNetCore.Mvc;


namespace API.Controllers
{
    public class PhotosController : BaseApiController
    {
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Add(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            // Convert the file to a base64 string
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var base64 = Convert.ToBase64String(memoryStream.ToArray());

            var command = new Add.Command { File = base64 };
            return HandleResult(await Mediator.Send(command));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            return HandleResult(await Mediator.Send(new Delete.Command { Id = id }));
        }

        [HttpPost("{id}/setMain")]
        public async Task<IActionResult> SetMain(string id)
        {
            return HandleResult(await Mediator.Send(new SetMain.Command { Id=id}));
        }
    }
}
