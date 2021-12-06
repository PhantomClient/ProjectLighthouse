#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Kettu;
using LBPUnion.ProjectLighthouse.Logging;
using LBPUnion.ProjectLighthouse.Types;
using LBPUnion.ProjectLighthouse.Types.Lists;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LBPUnion.ProjectLighthouse.Controllers
{
    [ApiController]
    [Route("LITTLEBIGPLANETPS3_XML/")]
    [Produces("text/xml")]
    public class PhotosController : ControllerBase
    {
        private readonly Database database;

        public PhotosController(Database database)
        {
            this.database = database;
        }

        [HttpPost("uploadPhoto")]
        public async Task<IActionResult> UploadPhoto()
        {
            User? user = await this.database.UserFromGameRequest(this.Request);
            if (user == null) return this.StatusCode(403, "");

            this.Request.Body.Position = 0;
            string bodyString = await new StreamReader(this.Request.Body).ReadToEndAsync();

            XmlSerializer serializer = new(typeof(Photo));
            Photo? photo = (Photo?)serializer.Deserialize(new StringReader(bodyString));
            if (photo == null) return this.BadRequest();

            photo.CreatorId = user.UserId;
            photo.Creator = user;

            foreach (PhotoSubject subject in photo.Subjects)
            {
                subject.User = await this.database.Users.FirstOrDefaultAsync(u => u.Username == subject.Username);

                if (subject.User == null) continue;

                subject.UserId = subject.User.UserId;
                Logger.Log($"Adding PhotoSubject (userid {subject.UserId}) to db", LoggerLevelPhotos.Instance);

                this.database.PhotoSubjects.Add(subject);
            }

            await this.database.SaveChangesAsync();

            photo.PhotoSubjectIds = photo.Subjects.Select(subject => subject.PhotoSubjectId.ToString()).ToArray();

            //            photo.Slot = await this.database.Slots.FirstOrDefaultAsync(s => s.SlotId == photo.SlotId);

            Logger.Log($"Adding PhotoSubjectCollection ({photo.PhotoSubjectCollection}) to photo", LoggerLevelPhotos.Instance);

            this.database.Photos.Add(photo);

            await this.database.SaveChangesAsync();

            return this.Ok();
        }

        [HttpGet("photos/user/{id:int}")]
        public async Task<IActionResult> SlotPhotos(int id)
        {
            List<Photo> photos = await this.database.Photos.Include(p => p.Creator).Take(10).ToListAsync();
            return this.Ok(new PhotoList(photos));
        }

        [HttpGet("photos/by")]
        public async Task<IActionResult> UserPhotosBy([FromQuery] string user, [FromQuery] int pageStart, [FromQuery] int pageSize)
        {
            User? userFromQuery = await this.database.Users.FirstOrDefaultAsync(u => u.Username == user);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (userFromQuery == null) return this.NotFound();

            List<Photo> photos = await this.database.Photos.Include
                    (p => p.Creator)
                .Where(p => p.CreatorId == userFromQuery.UserId)
                .OrderByDescending(s => s.Timestamp)
                .Skip(pageStart - 1)
                .Take(Math.Min(pageSize, 30))
                .ToListAsync();

            return this.Ok(new PhotoList(photos));
        }

        [HttpGet("photos/with")]
        public async Task<IActionResult> UserPhotosWith([FromQuery] string user, [FromQuery] int pageStart, [FromQuery] int pageSize)
        {
            User? userFromQuery = await this.database.Users.FirstOrDefaultAsync(u => u.Username == user);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (userFromQuery == null) return this.NotFound();

            List<Photo> photos = new();
            foreach (Photo photo in this.database.Photos.Include(p => p.Creator))
            {
                photos.AddRange(photo.Subjects.Where(subject => subject.User.UserId == userFromQuery.UserId).Select(_ => photo));
            }

            photos = photos.OrderByDescending(s => s.Timestamp).Skip(pageStart - 1).Take(Math.Min(pageSize, 30)).ToList();

            return this.Ok(new PhotoList(photos));
        }

        [HttpPost("deletePhoto/{id:int}")]
        public async Task<IActionResult> DeletePhoto(int id)
        {
            User? user = await this.database.UserFromGameRequest(this.Request);
            if (user == null) return this.StatusCode(403, "");

            Photo? photo = await this.database.Photos.FirstOrDefaultAsync(p => p.PhotoId == id);
            if (photo == null) return this.NotFound();
            if (photo.CreatorId != user.UserId) return this.StatusCode(401, "");

            this.database.Photos.Remove(photo);
            await this.database.SaveChangesAsync();
            return this.Ok();
        }
    }
}