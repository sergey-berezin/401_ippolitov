using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.IO;
using System.Linq;

namespace RecognitionAppWPF
{
    public class DatabaseManager : DbContext
    {
        public DbSet<Image> Images { get; set; }
        public DbSet<DetectedObject> Objects { get; set; }

        public string DbPath { get; set; }
        public DatabaseManager()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DbPath = System.IO.Path.Join(path, "images.db");
        }
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseLazyLoadingProxies().UseSqlite($"Data Source={DbPath}");

        public static int CountHash(byte[] data)
        {  
            const int prime = (int)1e9 + 3;
            const int aHash = 123456;
            const int bHash = 234567;
            int hash = 0;

            for (int i = 0; i < data.Length; i++)
            {
                hash <<= 1;
                hash += data[i];
                hash %= prime;
            }

            hash = (hash * aHash + bHash) % prime;
            return hash;
        }

        public bool ContainsImage(Image image)
        {
            var query = Images;
            foreach (var dbImage in query)
            {
                if (image.Equals(dbImage))
                    return true;
            }
            return false;
        }

        public void AddObject(Image image, DetectedObject obj) 
        {
            if (ContainsImage(image))
            {
                image.DetectedObjects.Add(obj);
                var dbImage = Images.Where(o => o.ImageHash == image.ImageHash && o.Content.SequenceEqual(image.Content)).First();
                if (dbImage.DetectedObjects.Where(o => obj.Equals(o)).Count() == 0)
                {
                    dbImage.DetectedObjects.Add(obj);
                }
            } else
            {
                image.DetectedObjects.Add(obj);
                Add(image);
            }

        }

    }

    public class Image
    {
        public int ImageId { get; set; }
        public int ImageHash { get; set; }
        public byte[] Content { get; set; }

        virtual public List<DetectedObject> DetectedObjects { get; set; } = new List<DetectedObject>();

        public bool Equals(Image other)
        {
            if (ImageHash != other.ImageHash)
            {
                return false;
            }
            return Content.SequenceEqual(other.Content);
        }
    }

    public class DetectedObject
    {
        public int DetectedObjectId { get; set; }
        public string ClassName { get; set; }
        public double Top { get; set; }
        public double Bottom { get; set; }
        public double Left { get; set; }
        public double Right { get; set; }

// public int ImageId { get; set; }

        public bool Equals(DetectedObject obj)
        {
            return (ClassName == obj.ClassName && Top == obj.Top &&
                Bottom == obj.Bottom && Left == obj.Left &&
                Right == obj.Right);
        }
        public override string ToString()
        {
            return ClassName + " detected at [" + Top.ToString() + ':' + Bottom.ToString() + ',' + Left.ToString() + ':' + Right.ToString() + "]";
        }
    }
}
