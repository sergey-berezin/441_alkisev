using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Windows.Navigation;
using System.Security.Cryptography;

namespace EfClasses
{
    public class Image
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Hash { get; set; }
        public byte[] Embedding { get; set; }
        public ImageDetails Details { get; set; }

        public static string GetHash(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                return string.Concat(sha256.ComputeHash(data).Select(x => x.ToString("X2")));
            }
        }
    }

    public class ImageDetails   
    {
        [Key]
        [ForeignKey(nameof(Image))]
        public int Id { get; set; }
        public byte[] Data { get; set; }
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
}
