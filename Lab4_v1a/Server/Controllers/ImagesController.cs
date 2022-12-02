using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Server.Database;

namespace Server.Controllers
{
    [ApiController]
    [Route("/[controller]")]
    public class ImagesController : ControllerBase
    {
        private IImagesDB db;
        public ImagesController(IImagesDB db)
        {
            this.db = db;
        }
        public Image[] GetImages()
        {
            return db.GetImages().ToArray();
        }

        [HttpGet("{id}")]
        public ActionResult<string> GetImage(int id)
        {
            if(id < 0)
            {
                return StatusCode(404, "Negative id");
            }
            return $"GetImage -> <image with id == {id}";
        }

        [HttpPost]
        public ActionResult<string> PostImage(Image img)
        {
            db.AddImage(img);
            return StatusCode(200, "Images added");
        }

        [HttpDelete]
        public ActionResult<string> DeleteImages()
        {
            db.DeleteImages();
            return StatusCode(200, "Images deleted");
        }
    }
}