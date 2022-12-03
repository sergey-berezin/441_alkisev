using System;
using Contracts;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.PixelFormats;
using FacesSimilarity;

namespace Server.Database
{
    public interface IImagesDB
    {
        public Task<IEnumerable<Image>> GetImages(CancellationToken ct);
        public Task<Image?> TryGetImage(int id, CancellationToken ct);
        public Task<int> AddImage(Image img, CancellationToken ct);
        public Task DeleteImages(CancellationToken ct);
    }

    public class ImagesContext: DbContext
    {
        public DbSet<Image> Images { get; set; }
        public DbSet<ImageDetails> Details { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder builder)
        {
            builder.UseSqlite("Data Source=images.db");
        }
    }

    public class ImagesDatabase : IImagesDB
    {
        public FacesComparator FacesComparator;
        public ImagesDatabase()
        {
            FacesComparator = new FacesComparator();
        }
        private SemaphoreSlim dbLock = new SemaphoreSlim(1, 1);

        async Task<int> IImagesDB.AddImage(Image img, CancellationToken ct)
        {
            var id = -1;
            try
            {
                await dbLock.WaitAsync();
                using (var db = new ImagesContext())
                {
                    string hash = Image.GetHash(img.Details.Data);
                    var q = db.Images.Where(x => x.Hash == hash)
                        .Include(x => x.Details)
                        .Where(x => Equals(x.Details.Data, img.Details.Data));
                    if(q.Any())
                    {
                        id = q.First().Id;
                    }
                }
            }
            catch(Exception e)
            {
                throw;
            }
            finally
            {
                dbLock.Release();
            }
            if(id != -1)
            {
                return id;
            }

            var stream = new MemoryStream(img.Details.Data);
            var imageSharp = await SixLabors.ImageSharp.Image.LoadAsync<Rgb24>(stream, ct);
            float[] embed = await FacesComparator.GetEmbeddingsAsync(imageSharp, ct);
            var byteArray = new byte[embed.Length * 4];
            Buffer.BlockCopy(embed, 0, byteArray, 0, byteArray.Length);
            img.Embedding = byteArray;

            try
            {
                await dbLock.WaitAsync(ct);
                using (var db = new ImagesContext())
                {
                    db.Add(img);
                    db.SaveChanges();
                }
            }
            catch (Exception e)
            {
                throw;
            }
            finally
            {
                dbLock.Release();
            }
            return img.Id;
        }

        async Task IImagesDB.DeleteImages(CancellationToken ct)
        {
            try
            {
                await dbLock.WaitAsync(ct);
                using (var db = new ImagesContext())
                {
                    db.Details.RemoveRange(db.Details);
                    db.Images.RemoveRange(db.Images);
                    db.SaveChanges();
                }
            }
            catch (Exception e)
            {
                throw;
            }
            finally
            {
                dbLock.Release();
            }
        }

        async Task<IEnumerable<Image>> IImagesDB.GetImages(CancellationToken ct)
        {
            IEnumerable<Image> ret;
            try
            {
                await dbLock.WaitAsync(ct);
                using (var db = new ImagesContext())
                {
                    ret = db.Images.Include(item => item.Details).ToList();
                }
            }
            catch (Exception e)
            {
                throw;
            }
            finally
            {
                dbLock.Release();
            }
            return ret;
        }

        async Task<Image?> IImagesDB.TryGetImage(int id, CancellationToken ct)
        {
            Image? img = null;
            try
            {
                await dbLock.WaitAsync(ct);
                using (var db = new ImagesContext())
                {
                    var q = db.Images.Where(x => x.Id == id);
                    if(q.Any())
                    {
                        img = q.First();
                    }
                }
            }
            catch(Exception e)
            {
                throw;
            }
            finally
            {
                dbLock.Release();
            }
            return img;
        }
    }

}