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
        private CancellationTokenSource cts;
        public ImagesController(IImagesDB db)
        {
            this.db = db;
            this.cts = new CancellationTokenSource();
        }
        public async Task<ActionResult<Image[]>> GetImages()
        {
            Image[] ret;
            try
            {
                ret = (await db.GetImages(cts.Token)).ToArray();
            }
            catch(Exception e)
            {
                return StatusCode(500, e.Message);
            }
            return ret;
        }

        [HttpGet("{id}")]
        public async Task <ActionResult<Image>> GetImage(int id)
        {
            Image? img;
            try
            {
                img = await db.TryGetImage(id, cts.Token);
            }
            catch(Exception e)
            {
                return StatusCode(500, e.Message);
            }
            if(img is null){
                return StatusCode(404, $"Not found image with id={id}");
            }
            return img;
        }

        [HttpPost]
        public async Task<ActionResult<string>> PostImage(Image img)
        {
            int id;
            try
            {
                if(img.Hash is null){
                    img.Hash = Image.GetHash(img.Details.Data);
                }
                id = await db.AddImage(img, cts.Token);
            }
            catch(Exception e)
            {
                return StatusCode(500, e.Message);
            }
            return StatusCode(200, $"{id}");
        }

        [HttpDelete]
        public async Task<ActionResult<string>> DeleteImages()
        {
            try
            {
                await db.DeleteImages(cts.Token);
            }
            catch(Exception e)
            {
                return StatusCode(500, e.Message);
            }
            return StatusCode(200, "Images deleted");
        }
    }
}