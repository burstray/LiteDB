using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using LiteDB.Engine;

namespace JIAVR
{
    internal class Program
    {
        static void Main(string[] args)
        {

            //FileStream fileStream = new FileStream("G:\\TestVR\\tt\\output\\videos\\IMG_4865.MP4", FileMode.Open, FileAccess.Read, FileShare.Read); //打开文件

            //byte[] bytes = new byte[fileStream.Length];
            //fileStream.Read(bytes, 0, bytes.Length);
            //fileStream.Close();

            //FileStreamFactory fsf = new FileStreamFactory("G:\\TestVR\\liteaes.db", "123456", false, false);

            //AesStream aes = (AesStream)fsf.GetStream(true, true);
            //aes.Write(bytes, 0, bytes.Length);
            //aes.Flush();


            FileStreamFactory fsf = new FileStreamFactory("G:\\TestVR\\liteaes.db", "123456", true, false);
            Stream ab = fsf.GetStream(false, true);
            ab.Seek(4096+32, SeekOrigin.Begin);
            byte[] byt = new byte[ab.Length];
            ab.Read(byt, 0, byt.Length);

            Console.ReadKey();
        }
    }
}
