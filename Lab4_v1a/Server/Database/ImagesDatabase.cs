using System;
using Contracts;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.PixelFormats;
using FacesSimilarity;

namespace Server.Database
{
    public interface IImagesDB
    {
        public IEnumerable<Image> GetImages();
        public Image? TryGetImage(int id);
        public int AddImage(Image img);
        public void DeleteImages();
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

        int IImagesDB.AddImage(Image img)
        {
            try
            {
                var stream = new MemoryStream(img.Details.Data);
                var imageSharp = SixLabors.ImageSharp.Image.Load<Rgb24>(stream);
                float[] embed = FacesComparator.GetEmbeddings(imageSharp);
                var byteArray = new byte[embed.Length * 4];
                Buffer.BlockCopy(embed, 0, byteArray, 0, byteArray.Length);
                img.Embedding = byteArray;
                
                dbLock.Wait();
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

        void IImagesDB.DeleteImages()
        {
            try
            {
                dbLock.Wait();
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

        IEnumerable<Image> IImagesDB.GetImages()
        {
            IEnumerable<Image> ret;
            try
            {
                dbLock.Wait();
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

        Image? IImagesDB.TryGetImage(int id)
        {
            Image img = null;
            try
            {
                dbLock.Wait();
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